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
        private static readonly bool IgnoreCase = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        private readonly PythonLanguageVersion _pythonLanguageVersion;
        private readonly Node[] _roots;
        public int Version { get; }

        public PathResolverSnapshot(PythonLanguageVersion pythonLanguageVersion) {
            _pythonLanguageVersion = pythonLanguageVersion;
            _roots = Array.Empty<Node>();
            Version = 0;
        }

        private PathResolverSnapshot(PythonLanguageVersion pythonLanguageVersion, Node[] roots, int version) {
            _pythonLanguageVersion = pythonLanguageVersion;
            _roots = roots;
            Version = version;
        }

        public IAvailableImports GetAvailableImportsFromAbsolutePath(in string modulePath, in IEnumerable<string> path) {
            if (!TryFindModule(modulePath, out var lastArc, out _, out _)) {
                return default;
            }

            //TODO: handle implicit relative path for 2X, use TryAppendPackages(ref lastArc, path)
            var firstArc = lastArc.GetFirstArc();
            if (!TryAppendPackages(firstArc, path, out lastArc)) {
                return default;
            }

            return GetAvailableImports(lastArc);
        }

        public IAvailableImports GetAvailableImportsFromRelativePath(in string modulePath, in int parentCount, in IEnumerable<string> relativePath) {
            if (!TryFindModule(modulePath, out var lastArc, out _, out _)) {
                // Module wasn't found in current snapshot
                return default;
            }

            if (parentCount > lastArc.PathLength - 1) {
                // Can't get outside of the root
                return default;
            }

            var relativeParentArc = lastArc.GetPrevious(parentCount);
            if (!TryAppendPackages(relativeParentArc, relativePath, out lastArc)) {
                return default;
            }
            
            return GetAvailableImports(lastArc);
        }

        private IAvailableImports GetAvailableImports(in Arc lastArc, in bool requireInitPy = true) {
            if (lastArc.End.IsModule) {
                return new AvailableModuleImports(lastArc.End.Name, lastArc.End.ModulePath);
            }

            var packageNode = lastArc.End;
            var initPy = packageNode.GetChild("__init__");
            if (initPy != default && initPy.IsModule) {
                // Ordinal package, return direct children
                return new AvailablePackageImports(packageNode.Name, packageNode.GetChildModules(), packageNode.GetChildPackageNames());
            }

            if (!_pythonLanguageVersion.IsImplicitNamespacePackagesSupported()) {
                return default;
            }

            return GetAvailableNamespaceImports(lastArc);
        }

        private IAvailableImports GetAvailableNamespaceImports(in Arc lastArc) {
            for (var i = 0; i < _roots.Length; i++) {
                if (TryMatchPath(lastArc, i, out var matchedLastArc)) {
                    if (matchedLastArc.End.IsModule) {
                        return new AvailableModuleImports(lastArc.End.Name, lastArc.End.ModulePath);
                    }

                    // TODO: add support for multiple matching paths. Handle cases:
                    // - Submodule with the same name is presented in several paths
                    // - One of the paths is module and another one is package
                    // - One of the packages contains __init__.py
                    return new AvailablePackageImports(lastArc.End.Name, lastArc.End.GetChildModules(), lastArc.End.GetChildPackageNames());
                }
            }

            return default;
        }

        public PathResolverSnapshot NewRoots(in IEnumerable<string> newRoots) {
            var newRootNodes = newRoots.Where(Path.IsPathRooted).Select(Node.CreateRoot).ToArray();
            return new PathResolverSnapshot(_pythonLanguageVersion, newRootNodes, Version + 1);
        }

        public PathResolverSnapshot AddModulePath(in string modulePath) {
            var isFound = TryFindModule(modulePath, out var lastArc, out var normalizedPath, out var unmatchedPathSpan);
            if (normalizedPath == default) {
                // Not a module
                return this;
            }

            if (isFound) {
                // Module is already added
                return this;
            }

            var newChildNode = CreateNewNodes(normalizedPath, unmatchedPathSpan, unmatchedPathSpan.length == 0 ? lastArc.End : default);
            if (unmatchedPathSpan.length == 0) {
                lastArc = lastArc.Previous;
            }

            var newEnd = lastArc.End.AddChild(newChildNode);
            var newRoot = UpdateNodesFromEnd(lastArc, newEnd);
            return ImmutableReplaceRoot(newRoot, lastArc.GetFirstArc().EndIndex);
        }

        public PathResolverSnapshot RemoveModulePath(in string modulePath) {
            if (!TryFindModule(modulePath, out var lastArc, out _, out _)) {
                // Module not found or found package in place of a module
                return this;
            }

            var moduleNode = lastArc.End;
            var moduleParent = lastArc.Start;
            var moduleIndex = lastArc.EndIndex;

            var newParent = moduleNode.ChildrenCount > 0 
                ? moduleParent.ReplaceChildAt(moduleNode.ToPackage(), moduleIndex) // preserve node as package
                : moduleParent.RemoveChildAt(moduleIndex);

            lastArc = lastArc.Previous;
            var newRoot = UpdateNodesFromEnd(lastArc, newParent);
            return ImmutableReplaceRoot(newRoot, lastArc.GetFirstArc().EndIndex);
        }

        private Arc MatchNodePath(in string normalizedModulePath, in int rootIndex, out (int start, int length) unmatchedPathSpan) {
            var root = _roots[rootIndex];
            var nameSpan = (start: 0, length: root.Name.Length);
            var modulePathLength = normalizedModulePath.LastIndexOf('.'); // exclude extension
            var lastArc = new Arc(rootIndex, root);
            
            while (normalizedModulePath.TryGetNextNonEmptySpan(Path.DirectorySeparatorChar, modulePathLength, ref nameSpan)) {
                var childIndex = lastArc.End.GetChildIndex(normalizedModulePath, nameSpan);
                if (childIndex == -1) {
                    break;
                }
                lastArc = lastArc.Append(childIndex);
            }

            unmatchedPathSpan = nameSpan.start != -1 ? (nameSpan.start, modulePathLength - nameSpan.start) : (-1, 0);
            return lastArc;
        }

        private static bool TryAppendPackages(in Arc arc, in IEnumerable<string> packageNames, out Arc lastArc) {
            lastArc = arc;
            foreach (var name in packageNames) {
                if (lastArc.End.IsModule) {
                    return false;
                }
                var index = lastArc.End.GetChildIndex(name);
                if (index == -1) {
                    return false;
                }
                lastArc = lastArc.Append(index);
            }

            return true;
        }

        private bool TryMatchPath(in Arc lastArc, in int rootIndex, out Arc matchedLastArc) {
            var sourceArc = lastArc.GetFirstArc();
            matchedLastArc = new Arc(rootIndex, _roots[rootIndex]);

            while (sourceArc != lastArc) {
                sourceArc = sourceArc.Next;
                var childIndex = matchedLastArc.End.GetChildIndex(sourceArc.End.Name);
                if (childIndex == -1) {
                    return false;
                }
                matchedLastArc = matchedLastArc.Append(childIndex);
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

        private static Node UpdateNodesFromEnd(Arc lastArc, Node newEnd) {
            while (lastArc.Start != default) {
                var newStart = lastArc.Start.ReplaceChildAt(newEnd, lastArc.EndIndex);
                lastArc = lastArc.Previous;
                newEnd = newStart;
            }

            return newEnd;
        }

        private bool TryFindModule(string modulePath, out Arc lastArc, out string normalizedPath, out (int start, int length) unmatchedPathSpan) {
            if (!Path.IsPathRooted(modulePath)) {
                throw new InvalidOperationException("Module path should be rooted");
            }

            normalizedPath = PathUtils.NormalizePath(modulePath);
            var rootIndex = 0;
            while (rootIndex < _roots.Length && !normalizedPath.StartsWithOrdinal(_roots[rootIndex].Name)) {
                rootIndex++;
            }

            if (rootIndex == _roots.Length) {
                throw new InvalidOperationException("File doesn't belong to any known search path");
            }

            if (!IsRootedPathEndsWithPythonFile(normalizedPath)) {
                lastArc = default;
                normalizedPath = default;
                unmatchedPathSpan = (-1, 0);
                return false;
            }

            lastArc = MatchNodePath(normalizedPath, rootIndex, out unmatchedPathSpan);
            return unmatchedPathSpan.length == 0 && lastArc.End.IsModule;
        }

        private static bool IsRootedPathEndsWithPythonFile(string rootedPath) {
            if (!rootedPath.EndsWithAnyOrdinal(new[] { ".py", ".pyi", ".pyw" }, IgnoreCase)) {
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
