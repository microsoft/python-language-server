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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.PythonTools.Analysis.DependencyResolution {
    internal partial struct PathResolverSnapshot {
        private static readonly bool IgnoreCaseInPaths = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        private static readonly StringComparison PathsStringComparison = IgnoreCaseInPaths ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

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
        private readonly string[] _interpreterSearchPaths;
        private readonly string[] _userSearchPaths;
        private readonly ImmutableArray<Node> _roots;
        public int Version { get; }

        public PathResolverSnapshot(PythonLanguageVersion pythonLanguageVersion) 
            : this(pythonLanguageVersion, string.Empty, Array.Empty<string>(), Array.Empty<string>(), ImmutableArray<Node>.Empty, Node.CreateDefaultRoot(), Node.CreateBuiltinRoot(), default) { }

        private PathResolverSnapshot(PythonLanguageVersion pythonLanguageVersion, string workDirectory, string[] userSearchPaths, string[] interpreterSearchPaths, ImmutableArray<Node> roots, Node nonRooted, Node builtins, int version) {
            _pythonLanguageVersion = pythonLanguageVersion;
            _workDirectory = workDirectory;
            _userSearchPaths = userSearchPaths;
            _interpreterSearchPaths = interpreterSearchPaths;
            _roots = roots;
            _nonRooted = nonRooted;
            _builtins = builtins;
            Version = version;
        }

        public string[] GetRootPaths() => _roots.Select(r => r.Name).ToArray();

        public IEnumerable<string> GetAllModuleNames() => GetModuleNames(_roots.Prepend(_nonRooted).Append(_builtins));
        public IEnumerable<string> GetModuleNamesFromSearchPaths() => GetModuleNames(_roots);

        private static IEnumerable<string> GetModuleNames(IEnumerable<Node> roots) => roots
            .SelectMany(r => r.TraverseBreadthFirst(n => n.IsModule ? Enumerable.Empty<Node>() : n.Children))
            .Where(n => n.IsModule)
            .Select(n => n.FullModuleName);

        public ModuleImport GetModuleImportFromModuleName(in string fullModuleName) {
            foreach (var root in _roots) {
                var node = root;
                var nameSpan = (start: 0, length: 0);
                while (fullModuleName.TryGetNextNonEmptySpan('.', ref nameSpan)) {
                    var childIndex = node.GetChildIndex(fullModuleName, nameSpan);
                    if (childIndex == -1) {
                        break;
                    }
                    node = node.Children[childIndex];
                }

                if (nameSpan.start == -1 && TryCreateModuleImport(root, node, out var moduleImports)) {
                    return moduleImports;
                }
            }

            if (fullModuleName.IndexOf('.') == -1 && TryFindNonRootedModule(fullModuleName, out var moduleImport)) {
                return moduleImport;
            }

            return default;
        }

        public IImportSearchResult GetImportsFromAbsoluteName(in string modulePath, in IEnumerable<string> fullName, bool forceAbsolute) {
            if (!TryFindModule(modulePath, out var lastEdge, out _, out _)) {
                return default;
            }

            var fullNameList = fullName.ToList();
            if (fullNameList.Count == 1 && lastEdge.IsNonRooted && TryFindNonRootedModule(fullNameList[0], out var moduleImport)) {
                return moduleImport;
            }

            var rootEdges = _roots.Select((r, i) => new Edge(i, r));
            if (!forceAbsolute) {
                rootEdges = rootEdges.Prepend(lastEdge.Previous);
            }

            if (TryFindImport(rootEdges, fullNameList, out var matchedEdges, out var shortestPath)) {
                return TryGetSearchResults(matchedEdges, out var searchResult) ? searchResult : new ImportNotFound(string.Join(".", fullNameList));
            }
            
            if (fullNameList.Count == 1 && _builtins.TryGetChild(fullNameList[0], out var builtin)) {
                return new ModuleImport(builtin.Name, builtin.FullModuleName, null, null);
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
                var rootPath = shortestPath.FirstEdge.End.Name;
                var existingModuleFullName = shortestPath.End.FullModuleName;
                var existingModuleFullPath = shortestPath.End.ModulePath;
                var remainingNameParts = fullNameList.Skip(shortestPath.PathLength - 1).ToList();
                return new PossibleModuleImport(lastEdge.End.FullModuleName, rootPath, existingModuleFullName, existingModuleFullPath, remainingNameParts);
            }

            return new ImportNotFound(string.Join(".", fullNameList));
        }

        public IImportSearchResult GetImportsFromRelativePath(in string modulePath, in int parentCount, in IEnumerable<string> relativePath) {
            if (!TryFindModule(modulePath, out var lastEdge, out _, out _)) {
                // Module wasn't found in current snapshot
                return default;
            }

            if (parentCount > lastEdge.PathLength) {
                // Can't get outside of the root
                return default;
            }

            var relativeParentEdge = lastEdge.GetPrevious(parentCount);
            var fullNameList = relativePath.ToList();;
            var rootEdges = _roots.Where((r, i) => RootContains(r, i, relativeParentEdge)).Select((r, i) => new Edge(i, r));

            if (TryFindImport(rootEdges, fullNameList, out var matchedEdges, out _) && TryGetSearchResults(matchedEdges, out var searchResult)) {
                return searchResult;
            }

            return CreateImportNotFound(relativeParentEdge, fullNameList);
        }

        private bool TryGetSearchResults(in ImmutableArray<Edge> matchedEdges, out IImportSearchResult searchResult) {
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

        private static bool TryCreateModuleImport(Edge lastEdge, out ModuleImport moduleImport)
            => TryCreateModuleImport(lastEdge.FirstEdge.End, lastEdge.End, out moduleImport);

        private static bool TryCreateModuleImport(Node rootNode, Node moduleNode, out ModuleImport moduleImport) {
            if (moduleNode.TryGetChild("__init__", out var initPyNode) && initPyNode.IsModule) {
                moduleImport = new ModuleImport(moduleNode.Name, initPyNode.FullModuleName, rootNode.Name, initPyNode.ModulePath);
                return true;
            }

            if (moduleNode.IsModule) {
                moduleImport = new ModuleImport(moduleNode.Name, moduleNode.FullModuleName, rootNode.Name, moduleNode.ModulePath);
                return true;
            }

            moduleImport = default;
            return false;
        }

        private bool TryFindNonRootedModule(string moduleName, out ModuleImport moduleImport) {
            foreach (var directoryNode in _nonRooted.Children) {
                if (directoryNode.TryGetChild(moduleName, out var nameNode)) {
                    moduleImport = new ModuleImport(moduleName, moduleName, directoryNode.Name, nameNode.ModulePath);
                    return true;
                }
            }

            moduleImport = default;
            return false;
        }

        private static bool TryCreateNamespacePackageImports(in ImmutableArray<Edge> matchedEdges, out IImportSearchResult searchResult) {
            if (matchedEdges.Count == 0) {
                searchResult = default;
                return false;
            }

            var modules = new List<ModuleImport>();
            var packageNames = new List<string>();

            foreach (var edge in matchedEdges) {
                var packageNode = edge.End;
                var rootNode = edge.FirstEdge.End;
                foreach (var child in packageNode.Children) {
                    if (TryCreateModuleImport(rootNode, child, out var moduleImports)) {
                        modules.Add(moduleImports);
                    } else {
                        packageNames.Add(child.Name);
                    }
                }
            }

            searchResult = new PackageImport(matchedEdges[0].End.Name, modules.Distinct().ToArray(), packageNames.Distinct().ToArray());
            return true;
        }

        private static ImportNotFound CreateImportNotFound(in Edge lastEdge, List<string> relativePath) {
            var fullName = GetFullModuleNameBuilder(lastEdge).Append(".", relativePath).ToString();
            return new ImportNotFound(fullName);
        }

        public PathResolverSnapshot SetWorkDirectory(in string workDirectory) {
            var normalizedRootDirectory = !string.IsNullOrEmpty(workDirectory) && Path.IsPathRooted(workDirectory)
                ? PathUtils.NormalizePath(workDirectory)
                : string.Empty;
            if (_workDirectory.Equals(normalizedRootDirectory, PathsStringComparison)) {
                return this;
            }

            var newRoots = CreateRoots(normalizedRootDirectory, _userSearchPaths, _interpreterSearchPaths);
            return new PathResolverSnapshot(_pythonLanguageVersion, normalizedRootDirectory, _userSearchPaths, _interpreterSearchPaths, newRoots, _nonRooted, _builtins, Version + 1);
        }

        public PathResolverSnapshot SetUserSearchPaths(in IEnumerable<string> searchPaths) {
            var userSearchPaths = searchPaths.ToArray();
            var newRoots = CreateRoots(_workDirectory, userSearchPaths, _interpreterSearchPaths);
            return new PathResolverSnapshot(_pythonLanguageVersion, _workDirectory, userSearchPaths, _interpreterSearchPaths, newRoots, _nonRooted, _builtins, Version + 1);
        }

        public PathResolverSnapshot SetInterpreterPaths(in IEnumerable<string> searchPaths) {
            var interpreterSearchPaths = searchPaths.ToArray();
            var newRoots = CreateRoots(_workDirectory, _userSearchPaths, interpreterSearchPaths);
            return new PathResolverSnapshot(_pythonLanguageVersion, _workDirectory, _userSearchPaths, interpreterSearchPaths, newRoots, _nonRooted, _builtins, Version + 1);
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
                _nonRooted, 
                Node.CreateBuiltinRoot(builtins), Version + 1);
        }

        private static ImmutableArray<Node> CreateRoots(string rootDirectory, string[] userSearchPaths, string[] interpreterSearchPaths) 
            => rootDirectory != string.Empty
                ? CreateRootsWithDefault(rootDirectory, userSearchPaths, interpreterSearchPaths)
                : CreateRootsWithoutDefault(userSearchPaths, interpreterSearchPaths);

        private static ImmutableArray<Node> CreateRootsWithDefault(string rootDirectory, string[] userSearchPaths, string[] interpreterSearchPaths) {
            var filteredUserSearchPaths = userSearchPaths.Select(FixPath)
                .Except(new [] { rootDirectory })
                .ToArray();

            var filteredInterpreterSearchPaths = interpreterSearchPaths.Select(FixPath)
                .Except(filteredUserSearchPaths.Prepend(rootDirectory))
                .ToArray();

            return ImmutableArray<Node>.Empty
                .Add(Node.CreateRoot(rootDirectory))
                .AddRange(filteredUserSearchPaths.Select(Node.CreateRoot).ToArray())
                .AddRange(filteredInterpreterSearchPaths.Select(Node.CreateRoot).ToArray());

            string FixPath(string p) => Path.IsPathRooted(p) ? PathUtils.NormalizePath(p) : PathUtils.NormalizePath(Path.Combine(rootDirectory, p));
        }

        private static ImmutableArray<Node> CreateRootsWithoutDefault(string[] userSearchPaths, string[] interpreterSearchPaths) {
            var filteredUserSearchPaths = userSearchPaths
                .Where(Path.IsPathRooted)
                .Select(PathUtils.NormalizePath)
                .ToArray();

            var filteredInterpreterSearchPaths = interpreterSearchPaths
                .Where(Path.IsPathRooted)
                .Select(PathUtils.NormalizePath)
                .Except(filteredUserSearchPaths)
                .ToArray();

            return ImmutableArray<Node>.Empty
                .AddRange(filteredUserSearchPaths.Select(Node.CreateRoot).ToArray())
                .AddRange(filteredInterpreterSearchPaths.Select(Node.CreateRoot).ToArray());
        }

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

            if (lastEdge.IsNonRooted) {
                return ReplaceNonRooted(AddToNonRooted(lastEdge, normalizedPath, unmatchedPathSpan, out fullModuleName));
            }

            var newChildNode = CreateNewNodes(lastEdge, normalizedPath, unmatchedPathSpan, out fullModuleName);
            if (unmatchedPathSpan.length == 0) {
                lastEdge = lastEdge.Previous;
            }

            var newEnd = lastEdge.End.AddChild(newChildNode);
            var newRoot = UpdateNodesFromEnd(lastEdge, newEnd);
            return ImmutableReplaceRoot(newRoot, lastEdge.FirstEdge.EndIndex);
        }

        public PathResolverSnapshot RemoveModulePath(in string modulePath) {
            if (!TryFindModule(modulePath, out var lastEdge, out _, out _)) {
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

        private bool MatchNodePathInNonRooted(string normalizedPath, out Edge lastEdge, out (int start, int length) unmatchedPathSpan) {
            var root = _nonRooted;
            var moduleNameStart = GetModuleNameStart(normalizedPath);
            var moduleNameEnd = GetModuleNameEnd(normalizedPath);
            var directorySpan = (0, moduleNameStart - 1);
            var nameSpan = (moduleNameStart, moduleNameEnd - moduleNameStart);

            var directoryNodeIndex = _nonRooted.GetChildIndex(normalizedPath, directorySpan);
            lastEdge = new Edge(0, root);
            if (directoryNodeIndex == -1) {
                unmatchedPathSpan = (0, moduleNameEnd);
                return false;
            }

            lastEdge = lastEdge.Append(directoryNodeIndex);
            var nameNodeIndex = lastEdge.End.GetChildIndex(normalizedPath, nameSpan);
            if (nameNodeIndex == -1) {
                unmatchedPathSpan = nameSpan;
                return false;
            }

            lastEdge = lastEdge.Append(nameNodeIndex);
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

            return shortestPath.IsEmpty;
        }

        private static bool TryFindName(in Edge edge, in IEnumerable<string> nameParts, out Edge lastEdge) {
            lastEdge = edge;
            foreach (var name in nameParts) {
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

        private static bool RootContains(in Node root, in int rootIndex, in Edge lastEdge) {
            var sourceEdge = lastEdge.FirstEdge;
            var targetEdge = new Edge(rootIndex, root);

            while (sourceEdge != lastEdge) {
                var sourceNode = sourceEdge.End;
                var targetNode = targetEdge.End;

                var nextSourceNode = sourceEdge.Next.End;
                var childIndex = targetNode.GetChildIndex(nextSourceNode.Name);
                if (childIndex == -1) {
                    return false;
                }

                sourceEdge = sourceEdge.Next;
                targetEdge = targetEdge.Append(childIndex);
            }

            return true;
        }

        private Node AddToNonRooted(in Edge lastEdge, in string modulePath, in (int start, int length) unmatchedPathSpan, out string fullModuleName) {
            var moduleNameStart = GetModuleNameStart(modulePath);
            var directoryIsKnown = unmatchedPathSpan.start == moduleNameStart;

            fullModuleName = directoryIsKnown
                ? modulePath.Substring(moduleNameStart, unmatchedPathSpan.length)
                : modulePath.Substring(moduleNameStart, unmatchedPathSpan.length - moduleNameStart);

            var moduleNode = Node.CreateModule(fullModuleName, modulePath, fullModuleName);

            if (!directoryIsKnown) {
                var directory = modulePath.Substring(0, moduleNameStart - 1);
                return _nonRooted.AddChild(new Node(directory, moduleNode));
            }

            var directoryIndex = lastEdge.EndIndex;
            var directoryNode = lastEdge.End.AddChild(moduleNode);
            return _nonRooted.ReplaceChildAt(directoryNode, directoryIndex);
        }

        private static Node CreateNewNodes(in Edge lastEdge, in string modulePath, in (int start, int length) unmatchedPathSpan, out string fullModuleName) {
            if (unmatchedPathSpan.length == 0) {
                // Module name matches name of existing package
                fullModuleName = GetFullModuleName(lastEdge);
                return lastEdge.End.ToModuleNode(modulePath, fullModuleName);
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
                var packageName = 
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
                sb.Append('.');
            }

            sb.Append(".", names, 0, names.Length - 1);
            AppendNameIfNotInitPy(sb, names.Last());
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
            while (rootIndex < _roots.Count && !normalizedPath.StartsWithOrdinal(_roots[rootIndex].Name, IgnoreCaseInPaths)) {
                rootIndex++;
            }

            // Search for module in root, or in 
            return rootIndex < _roots.Count 
                ? MatchNodePath(normalizedPath, rootIndex, out lastEdge, out unmatchedPathSpan) 
                : MatchNodePathInNonRooted(normalizedPath, out lastEdge, out unmatchedPathSpan);
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

        private PathResolverSnapshot ReplaceNonRooted(Node nonRooted) 
            => new PathResolverSnapshot(_pythonLanguageVersion, _workDirectory, _userSearchPaths, _interpreterSearchPaths, _roots, nonRooted, _builtins, Version + 1);

        private PathResolverSnapshot ImmutableReplaceRoot(Node root, int index) 
            => new PathResolverSnapshot(_pythonLanguageVersion, _workDirectory, _userSearchPaths, _interpreterSearchPaths, _roots.ReplaceAt(index, root), _nonRooted, _builtins, Version + 1);
    }
}
