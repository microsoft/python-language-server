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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.Analysis.Core.DependencyResolution {
    public partial class PathResolverSnapshot {
        // This root contains module paths that don't belong to any known search path. 
        // The directory of the module is stored on the first level, and name is stored on the second level
        // For example, "c:\dir\sub_dir2\module1.py" will be stored like this:
        //
        //                                  [*] 
        //        ┌────────────────╔═════════╝────────┬─────────────────┐
        // [c:\dir\sub_dir] [c:\dir\sub_dir2] [c:\dir2\sub_dir] [c:\dir2\sub_dir2]
        //          ╔═════════╤════╝────┬─────────┐
        //      [module1] [module2] [module3] [module4]
        //
        // When search paths changes, nodes may be added or removed from this subtree
        private readonly Node _nonRooted;

        // This node contains available builtin modules.
        private readonly Node _builtins;
        private readonly PythonLanguageVersion _pythonLanguageVersion;
        private readonly string _workDirectory;
        private readonly ImmutableArray<string> _interpreterSearchPaths;
        private readonly ImmutableArray<string> _userSearchPaths;
        private readonly ImmutableArray<Node> _roots;
        private readonly int _userRootsCount;
        public int Version { get; }

        public PathResolverSnapshot(PythonLanguageVersion pythonLanguageVersion, string workDirectory, ImmutableArray<string> interpreterSearchPaths, ImmutableArray<string> userSearchPaths)
            : this(pythonLanguageVersion, workDirectory, userSearchPaths, interpreterSearchPaths, ImmutableArray<Node>.Empty, 0, Node.CreateDefaultRoot(), Node.CreateBuiltinRoot(), default) {

            _pythonLanguageVersion = pythonLanguageVersion;
            _workDirectory = workDirectory;
            _userSearchPaths = userSearchPaths;
            _interpreterSearchPaths = interpreterSearchPaths;

            if (workDirectory != string.Empty) {
                CreateRootsWithDefault(workDirectory, userSearchPaths, interpreterSearchPaths, out _roots, out _userRootsCount);
            } else {
                CreateRootsWithoutDefault(userSearchPaths, interpreterSearchPaths, out _roots, out _userRootsCount);
            }

            _nonRooted = Node.CreateDefaultRoot();
            _builtins = Node.CreateDefaultRoot();
            Version = default;
        }

        private PathResolverSnapshot(PythonLanguageVersion pythonLanguageVersion, string workDirectory, ImmutableArray<string> userSearchPaths, ImmutableArray<string> interpreterSearchPaths, ImmutableArray<Node> roots, int userRootsCount, Node nonRooted, Node builtins, int version) {
            _pythonLanguageVersion = pythonLanguageVersion;
            _workDirectory = workDirectory;
            _userSearchPaths = userSearchPaths;
            _interpreterSearchPaths = interpreterSearchPaths;
            _roots = roots;
            _userRootsCount = userRootsCount;
            _nonRooted = nonRooted;
            _builtins = builtins;
            Version = version;
        }

        public ImmutableArray<string> GetAllImportableModuleNames() {
            var roots = _roots.Prepend(_nonRooted);
            var items = new Queue<Node>(roots);
            var names = ImmutableArray<string>.Empty;
            
            while (items.Count > 0) {
                var item = items.Dequeue();
                names = names.Add(item.FullModuleName);
                foreach (var child in item.Children) {
                    items.Enqueue(child);
                }
            }

            foreach (var builtin in _builtins.Children) {
                names = names.Add(builtin.FullModuleName);
            }

            return names;
        }

        public ModuleImport GetModuleImportFromModuleName(in string fullModuleName) {
            for (var rootIndex = 0; rootIndex < _roots.Count; rootIndex++) {
                if (TryFindModuleByName(rootIndex, fullModuleName, out var lastEdge) && TryCreateModuleImport(lastEdge, out var moduleImports)) {
                    return moduleImports;
                }
            }

            if (fullModuleName.IndexOf('.') != -1) {
                return default;
            }

            if (TryFindNonRootedModule(fullModuleName, out var moduleImport)) {
                return moduleImport;
            }

            if (_builtins.TryGetChild(fullModuleName, out var builtin)) {
                return new ModuleImport(ChildrenSource.Empty, builtin.Name, builtin.FullModuleName, null, null, -1, true, true);
            }

            return default;
        }

        public IImportChildrenSource GetModuleParentFromModuleName(in string fullModuleName) {
            for (var rootIndex = 0; rootIndex < _roots.Count; rootIndex++) {
                if (TryFindModuleByName(rootIndex, fullModuleName, out var lastEdge)) {
                    var parentEdge = lastEdge.Previous;
                    if (TryGetSearchResults(parentEdge, out var searchResult)) {
                        return searchResult;
                    }

                    if (parentEdge.IsFirst) {
                        return new ImportRoot(new ChildrenSource(this, parentEdge), parentEdge.End.Name);
                    }
                }
            }

            if (fullModuleName.IndexOf('.') != -1) {
                return default;
            }

            for (var i = 0; i < _nonRooted.Children.Count; i++) {
                var directoryNode = _nonRooted.Children[i];
                if (directoryNode.GetChildIndex(fullModuleName) != -1) {
                    return new ChildrenSource(this, new Edge(i, directoryNode));
                }
            }

            return default;
        }

        private bool TryFindModuleByName(in int rootIndex, in string fullModuleName, out Edge lastEdge) {
            lastEdge = new Edge(rootIndex, _roots[rootIndex]);
            foreach (var nameSpan in fullModuleName.SplitIntoSpans('.')) {
                var moduleNode = lastEdge.End;
                var childIndex = moduleNode.GetChildIndex(nameSpan);
                if (childIndex == -1 || moduleNode.IsModule && !IsInitPyModule(moduleNode, out _)) {
                    return false;
                }
                lastEdge = lastEdge.Append(childIndex);
            }

            return true;
        }

        public IEnumerable<string> GetPossibleModuleStubPaths(in string fullModuleName) {
            var firstDotIndex = fullModuleName.IndexOf('.');
            if (firstDotIndex == -1) {
                return Array.Empty<string>();
            }

            var relativeStubPath = new StringBuilder(fullModuleName)
                .Replace('.', Path.DirectorySeparatorChar)
                .Insert(firstDotIndex, "-stubs")
                .Append(".pyi")
                .ToString();

            return _roots.Select(r => Path.Combine(r.Name, relativeStubPath));
        }

        [Pure]
        public IImportSearchResult GetImportsFromAbsoluteName(in string modulePath, in IEnumerable<string> fullName, bool forceAbsolute) {
            Edge lastEdge;
            if (modulePath == null) {
                lastEdge = default;
            } else if (!TryFindModule(modulePath, out lastEdge, out _)) {
                // Module wasn't found in current snapshot, but we still can search for some of the absolute paths
                lastEdge = default;
            }

            var fullNameList = fullName.TakeWhile(n => !string.IsNullOrEmpty(n)).ToList();
            if (fullNameList.Count == 1 && lastEdge.IsNonRooted && TryFindNonRootedModule(fullNameList[0], out var moduleImport)) {
                return moduleImport;
            }

            var rootEdges = _roots.Select((r, i) => new Edge(i, r));
            if (!lastEdge.IsEmpty && !forceAbsolute && !lastEdge.Previous.IsFirst) {
                rootEdges = rootEdges.Prepend(lastEdge.Previous);
            }

            if (TryFindImport(rootEdges, fullNameList, out var matchedEdges, out var shortestPath)) {
                if (TryGetSearchResults(matchedEdges, out var searchResult)) {
                    return searchResult;
                }

                return new ImportNotFound(string.Join(".", fullNameList));
            }

            if (fullNameList.Count == 1 && _builtins.TryGetChild(fullNameList[0], out var builtin)) {
                return new ModuleImport(ChildrenSource.Empty, builtin.Name, builtin.FullModuleName, null, null, -1, true, true);
            }

            // Special case for sys.modules
            // If import wasn't found, but first parts of the import point to a module,
            // it is possible that module defines a member that is also a module
            // and there is a matching sys.modules key
            // For example:
            //
            // ---module1.py---:
            // import module2.mod as mod
            // print(mod.VALUE)
            //
            // ---module2.py---:
            // import module3 as mod
            //
            // ---module3.py---:
            // import sys
            // sys.modules['module2.mod'] = None
            // VALUE = 42

            if (shortestPath.PathLength > 0 && shortestPath.End.IsModule) {
                var possibleFullName = string.Join(".", fullNameList);
                var rootPath = shortestPath.FirstEdge.End.Name;
                var existingModuleName = shortestPath.End.Name;
                var existingModuleFullName = shortestPath.End.FullModuleName;
                var existingModulePath = shortestPath.End.ModulePath;
                var remainingNameParts = fullNameList.Skip(shortestPath.PathLength - 1).ToList();
                return new PossibleModuleImport(possibleFullName, rootPath, existingModuleName, existingModuleFullName, existingModulePath, remainingNameParts);
            }

            return new ImportNotFound(string.Join(".", fullNameList));
        }

        public IImportSearchResult GetImportsFromRelativePath(in string modulePath, in int parentCount, in IEnumerable<string> relativePath) {
            if (modulePath == null || !TryFindModule(modulePath, out var lastEdge, out _)) {
                // Module wasn't found in current snapshot, can't search for relative path
                return default;
            }

            if (parentCount >= lastEdge.PathLength) {
                // Can't get outside of the root
                return new RelativeImportBeyondTopLevel(string.Join(".", relativePath));
            }

            var fullNameList = relativePath.TakeWhile(n => !string.IsNullOrEmpty(n)).ToList();
            if (lastEdge.IsNonRooted) {
                // Handle relative imports only for modules in the same folder
                if (parentCount > 1) {
                    return default;
                }

                if (parentCount == 1 && fullNameList.Count == 1 && lastEdge.Start.TryGetChild(fullNameList[0], out var nameNode)) {
                    return new ModuleImport(ChildrenSource.Empty, fullNameList[0], fullNameList[0], lastEdge.Start.Name, nameNode.ModulePath, nameNode.ModuleFileSize, IsPythonCompiled(nameNode.ModulePath), false);
                }

                return new ImportNotFound(new StringBuilder(lastEdge.Start.Name)
                    .Append(".")
                    .Append(".", fullNameList)
                    .ToString());
            }

            var relativeParentEdge = lastEdge.GetPrevious(parentCount);

            var rootEdges = new List<Edge>();
            if (relativeParentEdge.IsFirst) {
                rootEdges.Add(relativeParentEdge);
            } else {
                for (var i = 0; i < _roots.Count; i++) {
                    if (RootContains(i, relativeParentEdge, out var rootEdge)) {
                        rootEdges.Add(rootEdge);
                    }
                }
            }

            if (TryFindImport(rootEdges, fullNameList, out var matchedEdges, out _) && TryGetSearchResults(matchedEdges, out var searchResult)) {
                return searchResult;
            }

            if (relativeParentEdge.IsNonRooted) {
                return default;
            }

            var fullName = GetFullModuleNameBuilder(relativeParentEdge).Append(".", fullNameList).ToString();
            return new ImportNotFound(fullName);
        }

        public bool IsLibraryFile(string filePath)
            => TryFindModule(filePath, out var edge, out _) && IsLibraryPath(edge.FirstEdge.End.Name);

        private bool TryGetSearchResults(in Edge matchedEdge, out IImportChildrenSource searchResult)
            => TryGetSearchResults(ImmutableArray<Edge>.Create(matchedEdge), out searchResult);

        private bool TryGetSearchResults(in ImmutableArray<Edge> matchedEdges, out IImportChildrenSource searchResult) {
            foreach (var edge in matchedEdges) {
                if (TryCreateModuleImport(edge, out var moduleImport)) {
                    searchResult = moduleImport;
                    return true;
                }
            }

            if (_pythonLanguageVersion.IsImplicitNamespacePackagesSupported()) {
                return TryCreateNamespacePackageImports(matchedEdges, out searchResult);
            }

            searchResult = default;
            return false;
        }

        private bool TryCreateModuleImport(Edge lastEdge, out ModuleImport moduleImport) {
            var moduleNode = lastEdge.End;
            var rootNode = lastEdge.FirstEdge.End;

            if (IsInitPyModule(moduleNode, out var initPyNode)) {
                moduleImport = new ModuleImport(
                    new ChildrenSource(this, lastEdge),
                    moduleNode.Name,
                    initPyNode.FullModuleName,
                    rootNode.Name,
                    initPyNode.ModulePath,
                    initPyNode.ModuleFileSize,
                    false,
                    IsLibraryPath(rootNode.Name));

                return true;
            }

            if (moduleNode.IsModule) {
                moduleImport = new ModuleImport(
                    ChildrenSource.Empty,
                    moduleNode.Name,
                    moduleNode.FullModuleName,
                    rootNode.Name,
                    moduleNode.ModulePath,
                    moduleNode.ModuleFileSize,
                    IsPythonCompiled(moduleNode.ModulePath),
                    IsLibraryPath(rootNode.Name));

                return true;
            }

            moduleImport = default;
            return false;
        }

        private bool TryFindNonRootedModule(string moduleName, out ModuleImport moduleImport) {
            foreach (var directoryNode in _nonRooted.Children) {
                if (directoryNode.TryGetChild(moduleName, out var nameNode)) {
                    moduleImport = new ModuleImport(ChildrenSource.Empty, moduleName, moduleName, directoryNode.Name, nameNode.ModulePath, nameNode.ModuleFileSize, IsPythonCompiled(nameNode.ModulePath), false);
                    return true;
                }
            }

            moduleImport = default;
            return false;
        }

        private bool TryCreateNamespacePackageImports(in ImmutableArray<Edge> matchedEdges, out IImportChildrenSource searchResult) {
            foreach (var edge in matchedEdges) {
                if (edge.IsFirst) {
                    continue;
                }

                var importNode = edge.End;
                searchResult = new ImplicitPackageImport(new ChildrenSource(this, matchedEdges), importNode.Name, importNode.FullModuleName);
                return true;
            }

            searchResult = default;
            return false;
        }

        public PathResolverSnapshot SetBuiltins(in IEnumerable<string> builtinModuleNames) {
            var builtins = ImmutableArray<Node>.Empty
                .AddRange(builtinModuleNames.Select(Node.CreateBuiltinModule).ToArray());

            return new PathResolverSnapshot(
                _pythonLanguageVersion,
                _workDirectory,
                _userSearchPaths,
                _interpreterSearchPaths,
                _roots,
                _userRootsCount,
                _nonRooted,
                Node.CreateBuiltinRoot(builtins),
                Version + 1);
        }

        private void CreateRootsWithDefault(string rootDirectory, ImmutableArray<string> userSearchPaths, ImmutableArray<string> interpreterSearchPaths, out ImmutableArray<Node> nodes, out int userRootsCount) {
            var filteredUserSearchPaths = userSearchPaths.Select(FixPath)
                .Except(new[] { rootDirectory })
                .ToArray();

            var filteredInterpreterSearchPaths = interpreterSearchPaths.Select(FixPath)
                .Except(filteredUserSearchPaths.Append(rootDirectory))
                .ToArray();

            userRootsCount = filteredUserSearchPaths.Length + 1;
            nodes = AddRootsFromSearchPaths(rootDirectory, filteredUserSearchPaths, filteredInterpreterSearchPaths);

            string FixPath(string p) => Path.IsPathRooted(p) ? PathUtils.NormalizePath(p) : PathUtils.NormalizePath(Path.Combine(rootDirectory, p));
        }

        private void CreateRootsWithoutDefault(ImmutableArray<string> userSearchPaths, ImmutableArray<string> interpreterSearchPaths, out ImmutableArray<Node> nodes, out int userRootsCount) {
            var filteredUserSearchPaths = userSearchPaths
                .Where(Path.IsPathRooted)
                .Select(PathUtils.NormalizePath)
                .ToArray();

            var filteredInterpreterSearchPaths = interpreterSearchPaths
                .Where(Path.IsPathRooted)
                .Select(PathUtils.NormalizePath)
                .Except(filteredUserSearchPaths)
                .ToArray();

            userRootsCount = filteredUserSearchPaths.Length;
            nodes = AddRootsFromSearchPaths(filteredUserSearchPaths, filteredInterpreterSearchPaths);
        }

        private ImmutableArray<Node> AddRootsFromSearchPaths(string rootDirectory, string[] userSearchPaths, string[] interpreterSearchPaths) {
            return ImmutableArray<Node>.Empty
                .AddRange(userSearchPaths.Select(GetOrCreateRoot).ToArray())
                .Add(GetOrCreateRoot(rootDirectory))
                .AddRange(interpreterSearchPaths.Select(GetOrCreateRoot).ToArray());
        }

        private ImmutableArray<Node> AddRootsFromSearchPaths(string[] userSearchPaths, string[] interpreterSearchPaths) {
            return ImmutableArray<Node>.Empty
                .AddRange(userSearchPaths.Select(GetOrCreateRoot).ToArray())
                .AddRange(interpreterSearchPaths.Select(GetOrCreateRoot).ToArray());
        }

        private Node GetOrCreateRoot(string path)
            => _roots.FirstOrDefault(r => r.Name.PathEquals(path)) ?? Node.CreateRoot(path);

        public PathResolverSnapshot AddModulePath(in string modulePath, long moduleFileSize, in bool allowNonRooted, out string fullModuleName) {
            var isFound = TryFindModule(modulePath, out var lastEdge, out var unmatchedPathSpan);
            if (unmatchedPathSpan.Source == default || !allowNonRooted && lastEdge.IsNonRooted) {
                // Not a module
                fullModuleName = null;
                return this;
            }

            if (isFound) {
                // Module is already added
                fullModuleName = lastEdge.End.FullModuleName;
                return this;
            }

            if (lastEdge.IsNonRooted) {
                return ReplaceNonRooted(AddToNonRooted(lastEdge, unmatchedPathSpan, moduleFileSize, out fullModuleName));
            }

            var newEnd = CreateNewNodes(lastEdge, unmatchedPathSpan, moduleFileSize, out fullModuleName);
            var newRoot = UpdateNodesFromEnd(lastEdge, newEnd);
            return ImmutableReplaceRoot(newRoot, lastEdge.FirstEdge.EndIndex);
        }

        public PathResolverSnapshot RemoveModulePath(in string modulePath) {
            if (!TryFindModule(modulePath, out var lastEdge, out _)) {
                // Module not found or found package in place of a module
                return this;
            }

            var moduleNode = lastEdge.End;
            var moduleParent = lastEdge.Start;
            var moduleIndex = lastEdge.EndIndex;

            var newParent = moduleNode.Children.Count > 0
                ? moduleParent.ReplaceChildAt(moduleNode.ToPackage(), moduleIndex) // preserve node as package
                : moduleParent.RemoveChildAt(moduleIndex);

            if (lastEdge.IsNonRooted) {
                var directoryIndex = lastEdge.Previous.EndIndex;
                return ReplaceNonRooted(_nonRooted.ReplaceChildAt(newParent, directoryIndex));
            }

            lastEdge = lastEdge.Previous;
            var newRoot = UpdateNodesFromEnd(lastEdge, newParent);
            return ImmutableReplaceRoot(newRoot, lastEdge.FirstEdge.EndIndex);
        }

        private bool MatchNodePathInNonRooted(string normalizedPath, out Edge lastEdge, out StringSpan unmatchedPathSpan) {
            var root = _nonRooted;
            var moduleNameStart = GetModuleNameStart(normalizedPath);
            var moduleNameEnd = GetModuleNameEnd(normalizedPath);
            var directorySpan = normalizedPath.Slice(0, moduleNameStart - 1);
            var nameSpan = normalizedPath.Slice(moduleNameStart, moduleNameEnd - moduleNameStart);

            var directoryNodeIndex = _nonRooted.GetChildIndex(directorySpan);
            lastEdge = new Edge(0, root);
            if (directoryNodeIndex == -1) {
                unmatchedPathSpan = normalizedPath.Slice(0, moduleNameEnd);
                return false;
            }

            lastEdge = lastEdge.Append(directoryNodeIndex);
            var nameNodeIndex = lastEdge.End.GetChildIndex(nameSpan);
            if (nameNodeIndex == -1) {
                unmatchedPathSpan = nameSpan;
                return false;
            }

            lastEdge = lastEdge.Append(nameNodeIndex);
            unmatchedPathSpan = new StringSpan(normalizedPath, -1, 0);
            return true;
        }

        private bool MatchNodePath(in string normalizedModulePath, in int rootIndex, out Edge lastEdge, out StringSpan unmatchedPathSpan) {
            var root = _roots[rootIndex];
            var modulePathLength = GetModuleNameEnd(normalizedModulePath); // exclude extension
            lastEdge = new Edge(rootIndex, root);
            unmatchedPathSpan = new StringSpan(normalizedModulePath, -1, 0);

            foreach (var nameSpan in normalizedModulePath.SplitIntoSpans(Path.DirectorySeparatorChar, root.Name.Length, modulePathLength - root.Name.Length)) {
                var childIndex = lastEdge.End.GetChildIndex(nameSpan);
                if (childIndex == -1) {
                    unmatchedPathSpan = normalizedModulePath.Slice(nameSpan.Start, modulePathLength - nameSpan.Start);
                    break;
                }
                lastEdge = lastEdge.Append(childIndex);
            }

            return unmatchedPathSpan.Length == 0 && lastEdge.End.IsModule;
        }

        private static bool TryFindImport(IEnumerable<Edge> rootEdges, List<string> fullNameList, out ImmutableArray<Edge> matchedEdges, out Edge shortestPath) {
            shortestPath = default;
            matchedEdges = ImmutableArray<Edge>.Empty;

            foreach (var rootEdge in rootEdges) {
                if (TryFindName(rootEdge, fullNameList, out var lastEdge)) {
                    matchedEdges = matchedEdges.Add(lastEdge);
                }

                if (lastEdge.End.IsModule && (shortestPath.IsEmpty || lastEdge.PathLength < shortestPath.PathLength)) {
                    shortestPath = lastEdge;
                }
            }

            return matchedEdges.Count > 0;
        }

        private static bool TryFindName(in Edge edge, in List<string> nameParts, out Edge lastEdge) {
            lastEdge = edge;
            foreach (var name in nameParts) {
                if (lastEdge.End.IsModule && !IsInitPyModule(lastEdge.End, out _)) {
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

        private bool RootContains(in int rootIndex, in Edge lastEdge, out Edge rootEdge) {
            var root = _roots[rootIndex];
            var sourceEdge = lastEdge.FirstEdge;
            var targetEdge = new Edge(rootIndex, root);

            while (sourceEdge != lastEdge) {
                var targetNode = targetEdge.End;

                var nextSourceNode = sourceEdge.Next.End;
                var childIndex = targetNode.GetChildIndex(nextSourceNode.Name);
                if (childIndex == -1) {
                    rootEdge = default;
                    return false;
                }

                sourceEdge = sourceEdge.Next;
                targetEdge = targetEdge.Append(childIndex);
            }

            rootEdge = targetEdge;
            return true;
        }

        private Node AddToNonRooted(in Edge lastEdge, in StringSpan unmatchedPathSpan, in long moduleFileSize, out string fullModuleName) {
            var (modulePath, unmatchedPathStart, unmatchedPathLength) = unmatchedPathSpan;
            var moduleNameStart = GetModuleNameStart(modulePath);
            var directoryIsKnown = unmatchedPathStart == moduleNameStart;

            fullModuleName = directoryIsKnown
                ? modulePath.Substring(moduleNameStart, unmatchedPathLength)
                : modulePath.Substring(moduleNameStart, unmatchedPathLength - moduleNameStart);

            var moduleNode = Node.CreateModule(fullModuleName, modulePath, moduleFileSize, fullModuleName);

            if (!directoryIsKnown) {
                var directory = modulePath.Substring(0, moduleNameStart - 1);
                return _nonRooted.AddChild(Node.CreatePackage(directory, directory, moduleNode));
            }

            var directoryIndex = lastEdge.EndIndex;
            var directoryNode = lastEdge.End.AddChild(moduleNode);
            return _nonRooted.ReplaceChildAt(directoryNode, directoryIndex);
        }

        private static Node CreateNewNodes(in Edge lastEdge, in StringSpan unmatchedPathSpan, in long moduleFileSize, out string fullModuleName) {
            var (modulePath, unmatchedPathStart, unmatchedPathLength) = unmatchedPathSpan;
            if (unmatchedPathLength == 0) {
                // Module name matches name of existing package
                fullModuleName = GetFullModuleName(lastEdge);
                return lastEdge.End.ToModuleNode(modulePath, moduleFileSize, fullModuleName);
            }

            if (unmatchedPathStart == GetModuleNameStart(modulePath)) {
                // Module is added to existing package
                var name = modulePath.Substring(unmatchedPathStart, unmatchedPathLength);
                fullModuleName = GetFullModuleName(lastEdge, name);
                var newChildNode = Node.CreateModule(name, modulePath, moduleFileSize, fullModuleName);
                return lastEdge.End.AddChild(newChildNode);
            }

            var names = modulePath.Split(Path.DirectorySeparatorChar, unmatchedPathStart, unmatchedPathLength);
            fullModuleName = GetFullModuleName(lastEdge, names, names.Length - 1);
            var newNode = Node.CreateModule(names.Last(), modulePath, moduleFileSize, fullModuleName);

            for (var i = names.Length - 2; i >= 0; i--) {
                newNode = Node.CreatePackage(names[i], GetFullModuleName(lastEdge, names, i), newNode);
            }

            return lastEdge.End.AddChild(newNode);
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

        private static string GetFullModuleName(in Edge lastEdge, string[] names, int lastNameIndex) {
            var sb = GetFullModuleNameBuilder(lastEdge);
            if (!lastEdge.IsFirst) {
                AppendName(sb, lastEdge.End.Name);
                sb.Append('.');
            }

            sb.Append(".", names, 0, lastNameIndex);
            AppendNameIfNotInitPy(sb, names[lastNameIndex]);
            return sb.ToString();
        }

        private static StringBuilder GetFullModuleNameBuilder(in Edge lastEdge) {
            if (lastEdge.IsNonRooted) {
                throw new InvalidOperationException($"{nameof(GetFullModuleNameBuilder)} should be called only for real root!");
            }

            var edge = lastEdge.FirstEdge;
            var sb = new StringBuilder();
            if (lastEdge.IsFirst) {
                return sb;
            }

            edge = edge.Next;
            while (edge != lastEdge) {
                AppendName(sb, edge.End.Name);
                edge = edge.Next;
            }

            return sb;
        }

        private static void AppendNameIfNotInitPy(StringBuilder builder, string name) {
            if (IsNotInitPy(name)) {
                AppendName(builder, name);
            }
        }

        private static void AppendName(StringBuilder builder, string name)
            => builder.AppendIf(builder.Length > 0, ".").Append(name);

        private static Node UpdateNodesFromEnd(Edge lastEdge, Node newEnd) {
            while (lastEdge.Start != default) {
                var newStart = lastEdge.Start.ReplaceChildAt(newEnd, lastEdge.EndIndex);
                lastEdge = lastEdge.Previous;
                newEnd = newStart;
            }

            return newEnd;
        }

        private bool TryFindModule(string modulePath, out Edge lastEdge, out StringSpan unmatchedPathSpan) {
            lastEdge = default;
            unmatchedPathSpan = default;

            if (!Path.IsPathRooted(modulePath)) {
                return false;
            }

            var normalizedPath = PathUtils.NormalizePath(modulePath);
            if (!IsRootedPathEndsWithPythonFile(normalizedPath)) {
                lastEdge = default;
                unmatchedPathSpan = new StringSpan(null, -1, 0);
                return false;
            }

            var rootIndex = 0;
            while (rootIndex < _roots.Count) {
                var rootPath = _roots[rootIndex].Name;
                if (PathUtils.PathStartsWith(normalizedPath, rootPath) && IsRootedPathEndsWithValidNames(normalizedPath, rootPath.Length)) {
                    break;
                }

                rootIndex++;
            }

            // Search for module in root, or in 
            return rootIndex < _roots.Count
                ? MatchNodePath(normalizedPath, rootIndex, out lastEdge, out unmatchedPathSpan)
                : MatchNodePathInNonRooted(normalizedPath, out lastEdge, out unmatchedPathSpan);
        }

        private static bool IsRootedPathEndsWithValidNames(string rootedPath, int start) {
            var nameSpan = (start: 0, length: start);
            var modulePathLength = GetModuleNameEnd(rootedPath); // exclude extension

            while (rootedPath.TryGetNextNonEmptySpan(Path.DirectorySeparatorChar, modulePathLength, ref nameSpan)) {
                if (!IsValidIdentifier(rootedPath, nameSpan.start, nameSpan.length)) {
                    return false;
                }
            }

            return true;
        }

        private static bool IsRootedPathEndsWithPythonFile(string rootedPath) {
            if (!IsPythonFile(rootedPath) && !IsPythonCompiled(rootedPath)) {
                return false;
            }

            var moduleNameStart = GetModuleNameStart(rootedPath);
            var moduleNameLength = GetModuleNameEnd(rootedPath) - moduleNameStart;
            return IsValidIdentifier(rootedPath, moduleNameStart, moduleNameLength);
        }

        private static bool IsValidIdentifier(string str, int start, int length)
            => str[start].IsLatin1LetterOrUnderscore() && str.CharsAreLatin1LetterOrDigitOrUnderscore(start + 1, length - 1);

        private static bool IsPythonFile(string rootedPath)
            => rootedPath.PathEndsWithAny(".py", ".pyi", ".pyw");

        private static bool IsPythonCompiled(string rootedPath)
            => rootedPath.PathEndsWithAny(".pyd", ".so", ".dylib");

        private static int GetModuleNameStart(string rootedModulePath)
            => rootedModulePath.LastIndexOf(Path.DirectorySeparatorChar) + 1;

        private static int GetModuleNameEnd(string rootedModulePath)
            => IsPythonCompiled(rootedModulePath) ? rootedModulePath.IndexOf('.', GetModuleNameStart(rootedModulePath)) : rootedModulePath.LastIndexOf('.');

        private static bool IsNotInitPy(string name)
            => !name.EqualsOrdinal("__init__");

        private static bool IsNotInitPy(in Node node)
            => !node.IsModule || !node.Name.EqualsOrdinal("__init__");

        private static bool IsInitPyModule(in Node node, out Node initPyNode)
            => node.TryGetChild("__init__", out initPyNode) && initPyNode.IsModule;

        private bool IsLibraryPath(string rootPath)
            => !_userSearchPaths.Contains(rootPath, StringExtensions.PathsStringComparer)
               && _interpreterSearchPaths.Except(_userSearchPaths).Contains(rootPath, StringExtensions.PathsStringComparer);

        private PathResolverSnapshot ReplaceNonRooted(Node nonRooted)
            => new PathResolverSnapshot(_pythonLanguageVersion, _workDirectory, _userSearchPaths, _interpreterSearchPaths, _roots, _userRootsCount, nonRooted, _builtins, Version + 1);

        private PathResolverSnapshot ImmutableReplaceRoot(Node root, int index)
            => new PathResolverSnapshot(_pythonLanguageVersion, _workDirectory, _userSearchPaths, _interpreterSearchPaths, _roots.ReplaceAt(index, root), _userRootsCount, _nonRooted, _builtins, Version + 1);
    }
}
