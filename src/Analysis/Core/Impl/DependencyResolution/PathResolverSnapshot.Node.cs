﻿// Copyright(c) Microsoft Corporation
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

using System.Diagnostics;
using System.Text;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Text;

namespace Microsoft.Python.Analysis.Core.DependencyResolution {
    public partial class PathResolverSnapshot {
        [DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
        private class Node {
            public readonly ImmutableArray<Node> Children;
            public readonly string Name;
            public readonly string ModulePath;
            public readonly string FullModuleName;
            public readonly long ModuleFileSize;
            public bool IsModule => ModulePath != null;

            private Node(string name, ImmutableArray<Node> children, string modulePath, long moduleFileSize, string fullModuleName) {
                Name = name;
                Children = children;
                ModulePath = modulePath;
                ModuleFileSize = moduleFileSize;
                FullModuleName = fullModuleName;
            }

            public static Node CreateDefaultRoot() => new Node("*", ImmutableArray<Node>.Empty, null, -1, null);
            public static Node CreateBuiltinRoot() => new Node(":", ImmutableArray<Node>.Empty, null, -1, null);
            public static Node CreateBuiltinRoot(ImmutableArray<Node> builtins) => new Node(":", builtins, null, -1, null);
            public static Node CreateRoot(string path) => new Node(PathUtils.NormalizePath(path), ImmutableArray<Node>.Empty, null, -1, null);

            public static Node CreateModule(string name, string modulePath, long moduleFileSize, string fullModuleName)
                => new Node(name, ImmutableArray<Node>.Empty, modulePath, moduleFileSize, fullModuleName);

            public static Node CreatePackage(string name, string fullModuleName, Node child)
                => new Node(name, ImmutableArray<Node>.Empty.Add(child), null, -1, fullModuleName);

            public static Node CreateBuiltinModule(string name) => new Node(name, ImmutableArray<Node>.Empty, null, 0, name);
            public Node ToModuleNode(string modulePath, long moduleFileSize, string fullModuleName) => new Node(Name, Children, modulePath, moduleFileSize, fullModuleName);
            public Node ToPackage() => new Node(Name, Children, null, 0, FullModuleName);
            public Node AddChild(Node child) => new Node(Name, Children.Add(child), ModulePath, ModuleFileSize, FullModuleName);
            public Node ReplaceChildAt(Node child, int index) => new Node(Name, Children.ReplaceAt(index, child), ModulePath, ModuleFileSize, FullModuleName);
            public Node RemoveChildAt(int index) => new Node(Name, Children.RemoveAt(index), ModulePath, ModuleFileSize, FullModuleName);

            public bool TryGetChild(string childName, out Node child) {
                var index = GetChildIndex(childName);
                if (index == -1) {
                    child = default;
                    return false;
                }

                child = Children[index];
                return true;
            }

            public int GetChildIndex(string childName) => Children.IndexOf(childName, NameEquals);
            public int GetChildIndex(StringSpan nameSpan) => Children.IndexOf(nameSpan, NameEquals);

            private string DebuggerDisplay {
                get {
                    var sb = new StringBuilder();
                    GetDebuggerDisplay(sb, 0);
                    return sb.ToString();
                }
            }

            private void GetDebuggerDisplay(StringBuilder sb, int offset) {
                sb.Append(' ', offset).Append(IsModule ? Name : "[" + Name + "]").AppendLine();
                foreach (var child in Children) {
                    child.GetDebuggerDisplay(sb, offset + 2);
                }
            }

            private static bool NameEquals(Node n, string name) => n.Name.EqualsOrdinal(name);

            private static bool NameEquals(Node n, StringSpan span)
                    => n.Name.Length == span.Length && n.Name.EqualsOrdinal(0, span.Source, span.Start, span.Length);
        }

        private sealed class ChildrenSource : IImportChildrenSource {
            public static IImportChildrenSource Empty { get; } = new EmptyChildrenSource();

            private readonly PathResolverSnapshot _snapshot;
            private readonly ImmutableArray<Edge> _edges;

            public ChildrenSource(PathResolverSnapshot snapshot, Edge edge)
                : this(snapshot, ImmutableArray<Edge>.Create(edge)) {}

            public ChildrenSource(PathResolverSnapshot snapshot, ImmutableArray<Edge> edges) {
                _snapshot = snapshot;
                _edges = edges;
            }

            public ImmutableArray<string> GetChildrenNames() {
                var results = ImmutableArray<string>.Empty;

                foreach (var edge in _edges) {
                    var children = edge.End.Children;
                    foreach (var child in children) {
                        if (IsNotInitPy(child)) {
                            results = results.Add(child.Name);
                        }
                    }
                }

                return results;
            }

            public bool TryGetChildImport(string name, out IImportSearchResult child) {
                foreach (var edge in _edges) {
                    var index = edge.End.GetChildIndex(name);
                    if (index == -1) {
                        continue;
                    }

                    var childEdge = edge.Append(index);
                    if (_snapshot.TryCreateModuleImport(childEdge, out var moduleImport)) {
                        child = moduleImport;
                    } else {
                        child = new ImplicitPackageImport(new ChildrenSource(_snapshot, childEdge), childEdge.End.Name, childEdge.End.FullModuleName);
                    }

                    return true;
                }

                child = default;
                return false;
            }
        }

        private sealed class EmptyChildrenSource : IImportChildrenSource {
            public ImmutableArray<string> GetChildrenNames() => ImmutableArray<string>.Empty;
            public bool TryGetChildImport(string name, out IImportSearchResult child) {
                child = default;
                return false;
            }
        }
    }
}
