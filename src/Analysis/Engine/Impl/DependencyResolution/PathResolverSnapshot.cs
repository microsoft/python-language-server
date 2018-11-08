// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.PythonTools.Analysis.DependencyResolution {
    internal partial struct PathResolverSnapshot {
        private static readonly Node NullRoot = Node.CreateNullRoot();
        private static readonly bool IgnoreCaseInPaths = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        private static readonly StringComparison PathsStringComparison = IgnoreCaseInPaths ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        private readonly PythonLanguageVersion _pythonLanguageVersion;
        private readonly string _rootDirectory;
        private readonly string[] _interpreterSearchPaths;
        private readonly string[] _userSearchPaths;
        private readonly Node[] _roots;
        public int Version { get; }

        public PathResolverSnapshot(PythonLanguageVersion pythonLanguageVersion) 
            : this(pythonLanguageVersion, string.Empty, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<Node>(), default) { }

        private PathResolverSnapshot(PythonLanguageVersion pythonLanguageVersion, string rootDirectory, string[] userSearchPaths, string[] interpreterSearchPaths, Node[] roots, int version) {
            _pythonLanguageVersion = pythonLanguageVersion;
            _rootDirectory = rootDirectory;
            _userSearchPaths = userSearchPaths;
            _interpreterSearchPaths = interpreterSearchPaths;
            _roots = roots;
            Version = version;
        }

        public AvailableModuleImports GetModuleImportsFromModuleName(in string fullModuleName) {
            foreach (var root in _roots) {
                var node = root;
                var nameSpan = (start: 0, length: 0);
                while (fullModuleName.TryGetNextNonEmptySpan('.', ref nameSpan)) {
                    var childIndex = node.GetChildIndex(fullModuleName, nameSpan);
                    if (childIndex == -1) {
                        break;
                    }
                    node = node.GetChildAt(childIndex);
                }

                if (nameSpan.start == -1 && TryCreateModuleImports(root, node, out var moduleImports)) {
                    return moduleImports;
                }
            }

            return default;
        }

        public IAvailableImports GetAvailableImportsFromAbsolutePath(in string modulePath, in IEnumerable<string> path) {
            if (!TryFindModule(modulePath, out var lastEdge, out _, out _)) {
                return default;
            }

            //TODO: handle implicit relative path for 2X, use TryAppendPackages(ref lastEdge, path)
            var firstEdge = lastEdge.GetFirstEdge();
            if (!TryAppendPackages(firstEdge, path, out lastEdge)) {
                return default;
            }

            return GetAvailableImports(lastEdge);
        }

        public IAvailableImports GetAvailableImportsFromRelativePath(in string modulePath, in int parentCount, in IEnumerable<string> relativePath) {
            if (!TryFindModule(modulePath, out var lastEdge, out _, out _)) {
                // Module wasn't found in current snapshot
                return default;
            }

            if (parentCount > lastEdge.PathLength - 1) {
                // Can't get outside of the root
                return default;
            }

            var relativeParentArc = lastEdge.GetPrevious(parentCount);
            if (!TryAppendPackages(relativeParentArc, relativePath, out lastEdge)) {
                return default;
            }
            
            return GetAvailableImports(lastEdge);
        }

        private IAvailableImports GetAvailableImports(in Edge lastEdge) {
            if (TryCreateModuleImports(lastEdge, out var availableImports)) {
                return availableImports;
            }

            if (!_pythonLanguageVersion.IsImplicitNamespacePackagesSupported()) {
                return default;
            }

            return GetAvailableNamespaceImports(lastEdge);
        }


        private IAvailableImports GetAvailableNamespaceImports(in Edge lastEdge) {
            for (var i = 0; i < _roots.Length; i++) {
                if (TryMatchPath(lastEdge, i, out var matchedLastEdge)) {
                    if (TryCreateModuleImports(matchedLastEdge, out var availableImports)) {
                        return availableImports;
                    }

                    // TODO: add support for multiple matching paths. Handle cases:
                    // - Submodule with the same name is presented in several paths
                    // - One of the paths is module and another one is package
                    // - One of the packages contains __init__.py
                    return CreateNamespacePackageImports(lastEdge);
                }
            }

            return default;
        }

        private static bool TryCreateModuleImports(Edge lastEdge, out AvailableModuleImports availableImports)
            => TryCreateModuleImports(lastEdge.GetFirstEdge().End, lastEdge.End, out availableImports);

        private static bool TryCreateModuleImports(Node rootNode, Node moduleNode, out AvailableModuleImports availableImports) {
            if (moduleNode.IsModule) {
                availableImports = new AvailableModuleImports(moduleNode.Name, moduleNode.FullModuleName, rootNode.Name, moduleNode.ModulePath);
                return true;
            }

            var initPyNode = moduleNode.GetChild("__init__");
            if (initPyNode != default && initPyNode.IsModule) {
                availableImports = new AvailableModuleImports(moduleNode.Name, initPyNode.FullModuleName, rootNode.Name, initPyNode.ModulePath);
                return true;
            }

            availableImports = default;
            return false;
        }

        private static AvailablePackageImports CreateNamespacePackageImports(in Edge lastEdge) {
            var modules = new List<AvailableModuleImports>();
            var packageNames = new List<string>();
            var packageNode = lastEdge.End;
            var rootNode = lastEdge.GetFirstEdge().End;
            for (var i = 0; i < packageNode.ChildrenCount; i++) {
                var child = packageNode.GetChildAt(i);
                if (TryCreateModuleImports(rootNode, child, out var moduleImports)) {
                    modules.Add(moduleImports);
                } else {
                    packageNames.Add(child.Name);
                }
            }

            return new AvailablePackageImports(packageNode.Name, rootNode.Name, modules.ToArray(), packageNames.ToArray());
        }

        public PathResolverSnapshot SetRoot(in string rootDirectory) {
            var normalizedRootDirectory = !string.IsNullOrEmpty(rootDirectory) && Path.IsPathRooted(rootDirectory)
                ? PathUtils.NormalizePath(rootDirectory)
                : string.Empty;
            if (_rootDirectory.Equals(normalizedRootDirectory, PathsStringComparison)) {
                return this;
            }

            var newRoots = CreateRoots(normalizedRootDirectory, _userSearchPaths, _interpreterSearchPaths);
            return new PathResolverSnapshot(_pythonLanguageVersion, normalizedRootDirectory, _userSearchPaths, _interpreterSearchPaths, newRoots, Version + 1);
        }

        public PathResolverSnapshot SetUserSearchPaths(in IEnumerable<string> searchPaths) {
            var userSearchPaths = searchPaths.ToArray();
            var newRoots = CreateRoots(_rootDirectory, userSearchPaths, _interpreterSearchPaths);
            return new PathResolverSnapshot(_pythonLanguageVersion, _rootDirectory, userSearchPaths, _interpreterSearchPaths, newRoots, Version + 1);
        }

        public PathResolverSnapshot SetInterpreterPaths(in IEnumerable<string> searchPaths) {
            var interpreterSearchPaths = searchPaths.ToArray();
            var newRoots = CreateRoots(_rootDirectory, _userSearchPaths, interpreterSearchPaths);
            return new PathResolverSnapshot(_pythonLanguageVersion, _rootDirectory, _userSearchPaths, interpreterSearchPaths, newRoots, Version + 1);
        }

        private static Node[] CreateRoots(string rootDirectory, string[] userSearchPaths, string[] interpreterSearchPaths) 
            => rootDirectory != string.Empty
                ? CreateRootsWithDefault(rootDirectory, userSearchPaths, interpreterSearchPaths)
                : CreateRootsWithoutDefault(userSearchPaths, interpreterSearchPaths);

        private static Node[] CreateRootsWithDefault(string rootDirectory, string[] userSearchPaths, string[] interpreterSearchPaths) {
            var filteredUserSearchPaths = userSearchPaths.Select(FixPath)
                .Except(new [] { rootDirectory })
                .ToArray();

            var filteredInterpreterSearchPaths = interpreterSearchPaths.Select(FixPath)
                .Except(filteredUserSearchPaths.Prepend(rootDirectory))
                .ToArray();

            return CreateRootsFromFiltered(Node.CreateRoot(rootDirectory), filteredUserSearchPaths, filteredInterpreterSearchPaths);

            string FixPath(string p) => Path.IsPathRooted(p) ? PathUtils.NormalizePath(p) : PathUtils.NormalizePath(Path.Combine(rootDirectory, p));
        }

        private static Node[] CreateRootsWithoutDefault(string[] userSearchPaths, string[] interpreterSearchPaths) {
            var filteredUserSearchPaths = userSearchPaths
                .Where(Path.IsPathRooted)
                .Select(PathUtils.NormalizePath)
                .ToArray();

            var filteredInterpreterSearchPaths = interpreterSearchPaths
                .Where(Path.IsPathRooted)
                .Select(PathUtils.NormalizePath)
                .Except(filteredUserSearchPaths)
                .ToArray();

            return CreateRootsFromFiltered(NullRoot, filteredUserSearchPaths, filteredInterpreterSearchPaths);
        }

        private static Node[] CreateRootsFromFiltered(Node defaultRoot, string[] userSearchPaths, string[] interpreterSearchPaths) 
            => new []{ defaultRoot }
                .Concat(userSearchPaths.Select(Node.CreateRoot))
                .Concat(interpreterSearchPaths.Select(Node.CreateRoot))
                .ToArray();

        public PathResolverSnapshot AddModulePath(in string modulePath, out string fullModuleName) {
            var isFound = TryFindModule(modulePath, out var lastEdge, out var normalizedPath, out var unmatchedPathSpan);
            if (normalizedPath == default) {
                // Not a module
                fullModuleName = null;
                return this;
            }

            if (isFound) {
                // Module is already added
                fullModuleName = lastEdge.End.FullModuleName;
                return this;
            }

            if (lastEdge.End.IsNullRoot) {
                fullModuleName = modulePath.Substring(unmatchedPathSpan.start, unmatchedPathSpan.length);
                var node = Node.CreateModule(fullModuleName, modulePath, fullModuleName);
                return ImmutableReplaceRoot(lastEdge.End.AddChild(node), 0);
            }

            var newChildNode = CreateNewNodes(lastEdge, normalizedPath, unmatchedPathSpan, out fullModuleName);
            if (unmatchedPathSpan.length == 0) {
                lastEdge = lastEdge.Previous;
            }

            var newEnd = lastEdge.End.AddChild(newChildNode);
            var newRoot = UpdateNodesFromEnd(lastEdge, newEnd);
            return ImmutableReplaceRoot(newRoot, lastEdge.GetFirstEdge().EndIndex);
        }

        public PathResolverSnapshot RemoveModulePath(in string modulePath) {
            if (!TryFindModule(modulePath, out var lastEdge, out _, out _)) {
                // Module not found or found package in place of a module
                return this;
            }

            var moduleNode = lastEdge.End;
            var moduleParent = lastEdge.Start;
            var moduleIndex = lastEdge.EndIndex;

            var newParent = moduleNode.ChildrenCount > 0 
                ? moduleParent.ReplaceChildAt(moduleNode.ToPackage(), moduleIndex) // preserve node as package
                : moduleParent.RemoveChildAt(moduleIndex);

            lastEdge = lastEdge.Previous;
            var newRoot = UpdateNodesFromEnd(lastEdge, newParent);
            return ImmutableReplaceRoot(newRoot, lastEdge.GetFirstEdge().EndIndex);
        }

        private bool MatchNodePathInNullRoot(string normalizedPath, out Edge lastEdge, out (int start, int length) unmatchedPathSpan) {
            var root = _roots[0];
            var moduleNameStart = GetModuleNameStart(normalizedPath);
            var nameSpan = (moduleNameStart, GetModuleNameEnd(normalizedPath) - moduleNameStart);
            var nodeIndex = root.GetChildIndex(normalizedPath, nameSpan);

            lastEdge = new Edge(0, root);
            if (nodeIndex == -1) {
                unmatchedPathSpan = nameSpan;
                return false;
            }

            lastEdge = lastEdge.Append(nodeIndex);
            unmatchedPathSpan = (-1, 0);
            return true;
        }

        private bool MatchNodePath(in string normalizedModulePath, in int rootIndex, out Edge lastEdge, out (int start, int length) unmatchedPathSpan) {
            var root = _roots[rootIndex];
            var nameSpan = (start: 0, length: root.Name.Length);
            var modulePathLength = GetModuleNameEnd(normalizedModulePath); // exclude extension

            lastEdge = new Edge(rootIndex, root);
            while (normalizedModulePath.TryGetNextNonEmptySpan(Path.DirectorySeparatorChar, modulePathLength, ref nameSpan)) {
                var childIndex = lastEdge.End.GetChildIndex(normalizedModulePath, nameSpan);
                if (childIndex == -1) {
                    break;
                }
                lastEdge = lastEdge.Append(childIndex);
            }

            unmatchedPathSpan = nameSpan.start != -1 ? (nameSpan.start, modulePathLength - nameSpan.start) : (-1, 0);
            return unmatchedPathSpan.length == 0 && lastEdge.End.IsModule;
        }

        private static bool TryAppendPackages(in Edge edge, in IEnumerable<string> packageNames, out Edge lastEdge) {
            lastEdge = edge;
            foreach (var name in packageNames) {
                if (lastEdge.End.IsModule) {
                    return false;
                }
                var index = lastEdge.End.GetChildIndex(name);
                if (index == -1) {
                    return false;
                }
                lastEdge = lastEdge.Append(index);
            }

            return true;
        }

        private bool TryMatchPath(in Edge lastEdge, in int rootIndex, out Edge matchedLastEdge) {
            var sourceNext = lastEdge.GetFirstEdge();
            matchedLastEdge = new Edge(rootIndex, _roots[rootIndex]);

            while (sourceNext != lastEdge) {
                sourceNext = sourceNext.Next;
                var childIndex = matchedLastEdge.End.GetChildIndex(sourceNext.End.Name);
                if (childIndex == -1) {
                    return false;
                }
                matchedLastEdge = matchedLastEdge.Append(childIndex);
            }

            return true;
        }

        private static Node CreateNewNodes(in Edge lastEdge, in string modulePath, in (int start, int length) unmatchedPathSpan, out string fullModuleName) {
            if (unmatchedPathSpan.length == 0) {
                // Module name matches name of existing package
                fullModuleName = GetFullModuleName(lastEdge);
                return lastEdge.End.ToModule(modulePath, fullModuleName);
            }

            if (unmatchedPathSpan.start == GetModuleNameStart(modulePath)) {
                // Module is added to existing package
                var name = modulePath.Substring(unmatchedPathSpan.start, unmatchedPathSpan.length);
                fullModuleName = GetFullModuleName(lastEdge, name);
                return Node.CreateModule(name, modulePath, fullModuleName);
            }

            var names = modulePath.Split(Path.DirectorySeparatorChar, unmatchedPathSpan.start, unmatchedPathSpan.length);
            fullModuleName = GetFullModuleName(lastEdge, names);
            var newNode = Node.CreateModule(names.Last(), modulePath, fullModuleName);

            for (var i = names.Length - 2; i >= 0; i--) {
                newNode = new Node(names[i], newNode);
            }

            return newNode;
        }

        private static string GetFullModuleName(in Edge lastEdge) {
            if (lastEdge.IsFirst) {
                return string.Empty;
            }

            var sb = GetFullModuleNameBuilder(lastEdge);
            if (lastEdge.End.IsModule) {
                AppendNameIfNotInitPy(sb, lastEdge.End.Name);
            } else {
                AppendName(sb, lastEdge.End.Name);
            }

            return sb.ToString();
        }

        private static string GetFullModuleName(in Edge lastEdge, string name) {
            if (lastEdge.IsFirst) {
                return IsNotInitPy(name) ? name : string.Empty;
            }

            var sb = GetFullModuleNameBuilder(lastEdge);
            AppendName(sb, lastEdge.End.Name);
            AppendNameIfNotInitPy(sb, name);
            return sb.ToString();
        }

        private static string GetFullModuleName(in Edge lastEdge, string[] names) {
            var sb = GetFullModuleNameBuilder(lastEdge);
            if (!lastEdge.IsFirst) {
                AppendName(sb, lastEdge.End.Name);
                sb.Append('.').Append(".", names, 0, names.Length - 1);
            } else {
                sb.Append(".", names, 0, names.Length - 1);
            }
            
            AppendNameIfNotInitPy(sb, names.Last());
            return sb.ToString();
        }

        private static StringBuilder GetFullModuleNameBuilder(in Edge lastEdge) {
            var edge = lastEdge.GetFirstEdge();
            if (edge.End.IsNullRoot) {
                throw new InvalidOperationException($"{nameof(GetFullModuleNameBuilder)} should be called only for real root!");
            }

            var sb = new StringBuilder();
            if (lastEdge.IsFirst) {
                return sb;
            }

            edge = edge.Next;
            while (edge != lastEdge) {
                AppendName(sb, edge.End.Name);
                edge = edge.Next;
            };

            return sb;
        }

        private static void AppendNameIfNotInitPy(StringBuilder builder, string name) {
            if (IsNotInitPy(name)) {
                AppendName(builder, name);
            }
        }

        private static void AppendName(StringBuilder builder, string name) {
            builder.AppendIf(builder.Length > 0, ".").Append(name);
        }

        private static Node UpdateNodesFromEnd(Edge lastEdge, Node newEnd) {
            while (lastEdge.Start != default) {
                var newStart = lastEdge.Start.ReplaceChildAt(newEnd, lastEdge.EndIndex);
                lastEdge = lastEdge.Previous;
                newEnd = newStart;
            }

            return newEnd;
        }

        private bool TryFindModule(string modulePath, out Edge lastEdge, out string normalizedPath, out (int start, int length) unmatchedPathSpan) {
            if (!Path.IsPathRooted(modulePath)) {
                throw new InvalidOperationException("Module path should be rooted");
            }

            normalizedPath = PathUtils.NormalizePath(modulePath);
            if (!IsRootedPathEndsWithPythonFile(normalizedPath)) {
                lastEdge = default;
                normalizedPath = default;
                unmatchedPathSpan = (-1, 0);
                return false;
            }

            var rootIndex = 0;
            while (rootIndex < _roots.Length && !normalizedPath.StartsWithOrdinal(_roots[rootIndex].Name, IgnoreCaseInPaths)) {
                rootIndex++;
            }

            if (rootIndex == _roots.Length) {
                // Special case when root directory isn't specified.
                if (_roots[0].Name == NullRoot.Name && IsRootedPathEndsWithPythonFile(normalizedPath)) {
                    return MatchNodePathInNullRoot(normalizedPath, out lastEdge, out unmatchedPathSpan);
                }

                throw new InvalidOperationException($"File '{modulePath}' doesn't belong to any known search path");
            }

            return MatchNodePath(normalizedPath, rootIndex, out lastEdge, out unmatchedPathSpan);
        }

        private static bool IsRootedPathEndsWithPythonFile(string rootedPath) {
            if (!rootedPath.EndsWithAnyOrdinal(new[] { ".py", ".pyi", ".pyw" }, IgnoreCaseInPaths)) {
                return false;
            }

            var moduleNameStart = GetModuleNameStart(rootedPath);
            return rootedPath[moduleNameStart].IsLatin1LetterOrUnderscore()
                   && rootedPath.CharsAreLatin1LetterOrDigitOrUnderscore(moduleNameStart + 1, GetModuleNameEnd(rootedPath) - moduleNameStart - 1);
        }

        private static int GetModuleNameStart(string rootedModulePath) 
            => rootedModulePath.LastIndexOf(Path.DirectorySeparatorChar) + 1;

        private static int GetModuleNameEnd(string rootedModulePath) 
            => rootedModulePath.LastIndexOf('.');

        private static bool IsNotInitPy(string name)
            => !name.EqualsOrdinal("__init__");

        private PathResolverSnapshot ImmutableReplaceRoot(Node root, int index) 
            => new PathResolverSnapshot(_pythonLanguageVersion, _rootDirectory, _userSearchPaths, _interpreterSearchPaths, _roots.ImmutableReplaceAt(root, index), Version + 1);
    }
}
