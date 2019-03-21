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
    internal abstract class LocatedMember: ILocatedMember {
        private HashSet<Node> _references;

        protected LocatedMember(PythonMemberType memberType, Node definition = null): this(definition) {
            MemberType = memberType;
        }

        protected LocatedMember(Node definition = null) {
            Definition = definition;
        }

        public virtual PythonMemberType MemberType { get; }

        public virtual Node Definition { get; private set; }

        public virtual LocationInfo GetLocation(PythonAst ast)
            => Definition?.GetLocation(ast) ?? LocationInfo.Empty;

        public virtual IReadOnlyList<Node> References => _references?.ToArray() ?? Array.Empty<Node>();
        public void AddReference(Node node) {
            _references = _references ?? new HashSet<Node>();
            _references.Add(node);
        }

        internal virtual void SetDefinition(Node definition) => Definition = definition;
    }

    internal abstract class EmptyLocatedMember: ILocatedMember {
        protected EmptyLocatedMember(PythonMemberType memberType) {
            MemberType = memberType;
        }

        public PythonMemberType MemberType { get; }
        public Node Definition => null;
        public IReadOnlyList<Node> References => Array.Empty<Node>();
        public void AddReference(Node expression) { }
        public LocationInfo GetLocation(PythonAst ast) => LocationInfo.Empty;
    }
}
