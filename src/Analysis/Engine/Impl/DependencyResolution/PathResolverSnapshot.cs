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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Analysis.DependencyResolution {
    internal partial struct PathResolverSnapshot {
        private static readonly bool IgnoreCaseInPaths = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        private readonly PythonLanguageVersion _pythonLanguageVersion;
        private readonly Node[] _roots;
        public int Version { get; }

        public PathResolverSnapshot(PythonLanguageVersion pythonLanguageVersion) 
            : this(pythonLanguageVersion, Array.Empty<Node>(), default) { }

        private PathResolverSnapshot(PythonLanguageVersion pythonLanguageVersion, Node[] roots, int version) {
            _pythonLanguageVersion = pythonLanguageVersion;
            _roots = roots;
            Version = version;
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
            if (lastEdge.End.IsModule) {
                return new AvailableModuleImports(lastEdge.End.Name, lastEdge.End.ModulePath);
            }

            var packageNode = lastEdge.End;
            var initPy = packageNode.GetChild("__init__");
            if (initPy != default && initPy.IsModule) {
                // Ordinal package, return direct children
                return new AvailablePackageImports(packageNode.Name, packageNode.GetChildModules(), packageNode.GetChildPackageNames());
            }

            if (!_pythonLanguageVersion.IsImplicitNamespacePackagesSupported()) {
                return default;
            }

            return GetAvailableNamespaceImports(lastEdge);
        }

        private IAvailableImports GetAvailableNamespaceImports(in Edge lastEdge) {
            for (var i = 0; i < _roots.Length; i++) {
                if (TryMatchPath(lastEdge, i, out var matchedLastEdge)) {
                    if (matchedLastEdge.End.IsModule) {
                        return new AvailableModuleImports(lastEdge.End.Name, lastEdge.End.ModulePath);
                    }

                    // TODO: add support for multiple matching paths. Handle cases:
                    // - Submodule with the same name is presented in several paths
                    // - One of the paths is module and another one is package
                    // - One of the packages contains __init__.py
                    return new AvailablePackageImports(lastEdge.End.Name, lastEdge.End.GetChildModules(), lastEdge.End.GetChildPackageNames());
                }
            }

            return default;
        }

        public PathResolverSnapshot NewRoots(in IEnumerable<string> newRoots) {
            var newRootNodes = newRoots.Where(Path.IsPathRooted).Select(Node.CreateRoot).ToArray();
            return new PathResolverSnapshot(_pythonLanguageVersion, newRootNodes, Version + 1);
        }

        public PathResolverSnapshot AddModulePath(in string modulePath) {
            var isFound = TryFindModule(modulePath, out var lastEdge, out var normalizedPath, out var unmatchedPathSpan);
            if (normalizedPath == default) {
                // Not a module
                return this;
            }

            if (isFound) {
                // Module is already added
                return this;
            }

            var newChildNode = CreateNewNodes(normalizedPath, unmatchedPathSpan, unmatchedPathSpan.length == 0 ? lastEdge.End : default);
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

        private Edge MatchNodePath(in string normalizedModulePath, in int rootIndex, out (int start, int length) unmatchedPathSpan) {
            var root = _roots[rootIndex];
            var nameSpan = (start: 0, length: root.Name.Length);
            var modulePathLength = normalizedModulePath.LastIndexOf('.'); // exclude extension
            var lastEdge = new Edge(rootIndex, root);
            
            while (normalizedModulePath.TryGetNextNonEmptySpan(Path.DirectorySeparatorChar, modulePathLength, ref nameSpan)) {
                var childIndex = lastEdge.End.GetChildIndex(normalizedModulePath, nameSpan);
                if (childIndex == -1) {
                    break;
                }
                lastEdge = lastEdge.Append(childIndex);
            }

            unmatchedPathSpan = nameSpan.start != -1 ? (nameSpan.start, modulePathLength - nameSpan.start) : (-1, 0);
            return lastEdge;
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

        private static Node CreateNewNodes(string modulePath, (int start, int length) unmatchedPathSpan, Node packageNode) {
            if (packageNode != default) {
                // Module name matches name of existing package        
                return packageNode.ToModule(modulePath);
            }

            if (unmatchedPathSpan.start == modulePath.LastIndexOf(Path.DirectorySeparatorChar) + 1) {
                // Module is added to existing package
                return Node.CreateModule(modulePath.Substring(unmatchedPathSpan.start, unmatchedPathSpan.length), modulePath);
            }

            var names = modulePath.Split(Path.DirectorySeparatorChar, unmatchedPathSpan.start, unmatchedPathSpan.length);
            var newNode = Node.CreateModule(names.Last(), modulePath);

            for (var i = names.Length - 2; i >= 0; i--) {
                newNode = new Node(names[i], newNode);
            }

            return newNode;
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
            var rootIndex = 0;
            while (rootIndex < _roots.Length && !normalizedPath.StartsWithOrdinal(_roots[rootIndex].Name, IgnoreCaseInPaths)) {
                rootIndex++;
            }

            if (rootIndex == _roots.Length) {
                throw new InvalidOperationException($"File '{modulePath}' doesn't belong to any known search path");
            }

            if (!IsRootedPathEndsWithPythonFile(normalizedPath)) {
                lastEdge = default;
                normalizedPath = default;
                unmatchedPathSpan = (-1, 0);
                return false;
            }

            lastEdge = MatchNodePath(normalizedPath, rootIndex, out unmatchedPathSpan);
            return unmatchedPathSpan.length == 0 && lastEdge.End.IsModule;
        }

        private static bool IsRootedPathEndsWithPythonFile(string rootedPath) {
            if (!rootedPath.EndsWithAnyOrdinal(new[] { ".py", ".pyi", ".pyw" }, IgnoreCaseInPaths)) {
                return false;
            }

            var fileStartIndex = rootedPath.LastIndexOf(Path.DirectorySeparatorChar) + 1;
            return rootedPath[fileStartIndex].IsLatin1LetterOrUnderscore()
                   && rootedPath.CharsAreLatin1LetterOrDigitOrUnderscore(fileStartIndex + 1, rootedPath.LastIndexOf('.') - fileStartIndex - 1);
        }

        private PathResolverSnapshot ImmutableReplaceRoot(Node root, int index) 
            => new PathResolverSnapshot(_pythonLanguageVersion, _roots.ImmutableReplaceAt(root, index), Version + 1);
    }
}
