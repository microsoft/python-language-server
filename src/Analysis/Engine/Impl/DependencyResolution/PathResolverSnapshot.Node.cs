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
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.PythonTools.Analysis.Infrastructure;

namespace Microsoft.PythonTools.Analysis.DependencyResolution {
    internal partial struct PathResolverSnapshot {
        [DebuggerDisplay("{" + nameof(DebuggerDisplay) + ",nq}")]
        private class Node {
            private readonly Node[] _children;
            public readonly string Name;
            public readonly string ModulePath;
            public readonly string FullModuleName;
            public bool IsNullRoot => Name == "$";
            public bool IsModule => ModulePath != null;
            public int ChildrenCount => _children.Length;

            private Node(string name, Node[] children, string modulePath, string fullModuleName) {
                Name = name;
                _children = children;
                ModulePath = modulePath;
                FullModuleName = fullModuleName;
            }

            public static Node CreateNullRoot()
                => new Node("$", Array.Empty<Node>(), null, null);

            public static Node CreateRoot(string path) 
                => new Node(PathUtils.NormalizePath(path), Array.Empty<Node>(), null, null);

            public static Node CreateModule(string name, string modulePath, string fullModuleName)
                => new Node(name, Array.Empty<Node>(), modulePath, fullModuleName);

            public Node(string name, Node child) {
                Name = name;
                _children = new[] {child};
                ModulePath = null;
                FullModuleName = null;
            }

            public Node ToModule(string modulePath, string fullModuleName) => new Node(Name, _children, modulePath, fullModuleName);
            public Node ToPackage() => new Node(Name, _children, null, null);
            public Node AddChild(Node child) => new Node(Name, _children.ImmutableAdd(child), ModulePath, FullModuleName);
            public Node ReplaceChildAt(Node child, int index) => new Node(Name, _children.ImmutableReplaceAt(child, index), ModulePath, FullModuleName);
            public Node RemoveChildAt(int index) => new Node(Name, _children.ImmutableRemoveAt(index), ModulePath, FullModuleName);

            public Node GetChildAt(int index) => _children[index];
            public Node GetChild(string childName) {
                var index = GetChildIndex(childName);
                return index == -1 ? default : _children[index];
            }

            public int GetChildIndex(string childName) => _children.IndexOf(childName, NameEquals);

            public int GetChildIndex(string modulePath, (int start, int length) nameSpan) 
                => _children.IndexOf((modulePath, nameSpan.start, nameSpan.length), NameEquals);

            private string DebuggerDisplay {
                get {
                    var sb = new StringBuilder();
                    GetDebuggerDisplay(sb, 0);
                    return sb.ToString();
                }
            }

            private void GetDebuggerDisplay(StringBuilder sb, int offset) {
                sb.Append(' ', offset).Append(IsModule ? Name : "[" + Name + "]").AppendLine();
                foreach (var child in _children) {
                    child.GetDebuggerDisplay(sb, offset + 2);
                }
            }

            private static bool NameEquals(Node n, string name) => n.Name.EqualsOrdinal(name);

            private static bool NameEquals(Node n, (string str, int start, int length) span) 
                    => n.Name.EqualsOrdinal(0, span.str, span.start, span.length);
        }
    }
}
