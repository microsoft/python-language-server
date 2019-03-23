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
using System.Linq;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Types {
    internal abstract class LocatedMember : ILocatedMember {
        private struct Location {
            private readonly IPythonModule _module;
            private readonly Node _node;

            public Location(IPythonModule module, Node node) {
                _module = module;
                _node = node;
            }

            public LocationInfo LocationInfo {
                get {
                    if (_node is MemberExpression mex && _module.Analysis.Ast != null) {
                        var span = mex.GetNameSpan(_module.Analysis.Ast);
                        return new LocationInfo(_module.FilePath, _module.Uri, span);
                    }

                    return _node?.GetLocation(_module) ?? LocationInfo.Empty;
                }
            }

            public override bool Equals(object obj)
                => obj is Location other && other._node == _node;

            public override int GetHashCode() => _node.GetHashCode();
        }

        private HashSet<Location> _references;

        protected LocatedMember(PythonMemberType memberType, IPythonModule declaringModule, Node definition = null)
            : this(declaringModule, definition) {
            MemberType = memberType;
        }

        protected LocatedMember(IPythonModule declaringModule, Node definition = null) {
            DeclaringModule = declaringModule;
            DefinitionNode = definition;
        }

        protected void SetDeclaringModule(IPythonModule module) => DeclaringModule = module;
        public virtual PythonMemberType MemberType { get; }

        public virtual IPythonModule DeclaringModule { get; private set; }

        public virtual LocationInfo Definition => DefinitionNode.GetLocation(DeclaringModule);

        public Node DefinitionNode { get; private set; }

        public virtual IReadOnlyList<LocationInfo> References {
            get {
                if (_references == null) {
                    return new[] { Definition };
                }
                var refs = _references.Select(r => r.LocationInfo).OrderBy(x => x.Span);
                return Enumerable.Repeat(Definition, 1).Concat(refs).ToArray();
            }
        }

        public virtual void AddReference(IPythonModule module, Node location) {
            if (module != null && location != null) {
                _references = _references ?? new HashSet<Location>();
                _references.Add(new Location(module, location));
            }
        }

        internal virtual void SetDefinitionNode(Node definition) => DefinitionNode = definition;
    }

    internal abstract class EmptyLocatedMember : ILocatedMember {
        protected EmptyLocatedMember(PythonMemberType memberType) {
            MemberType = memberType;
        }

        public PythonMemberType MemberType { get; }
        public IPythonModule DeclaringModule => null;
        public LocationInfo Definition => LocationInfo.Empty;
        public Node DefinitionNode => null;
        public IReadOnlyList<LocationInfo> References => Array.Empty<LocationInfo>();
        public void AddReference(IPythonModule module, Node location) { }
    }
}
