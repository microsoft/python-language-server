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

using System.Collections.Generic;
using System.Linq;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Parsing.Extensions {
    public static class AstExtensions {
        public static IEnumerable<INode> ChildNodesDepthFirst(this INode node)
            => node.TraverseDepthFirst(n => n.GetChildNodes());

        public static IEnumerable<INode> SelectChildNodesDepthFirst(this IEnumerable<INode> nodes)
            => nodes.SelectMany(n => n.ChildNodesDepthFirst());

        public static IEnumerable<INode> ChildNodesBreadthFirst(this INode node)
            => node.TraverseBreadthFirst(n => n.GetChildNodes());

        public static IEnumerable<INode> SelectChildNodesBreadthFirst(this IEnumerable<INode> nodes)
            => nodes.SelectMany(n => n.ChildNodesBreadthFirst());

        #region Backwards compatibility
        
        public static IEnumerable<Node> ChildNodesDepthFirst(this Node node)
            => node.TraverseDepthFirst(n => n.GetChildNodes());

        public static IEnumerable<Node> SelectChildNodesDepthFirst(this IEnumerable<Node> nodes)
            => nodes.SelectMany(n => n.ChildNodesDepthFirst());

        public static IEnumerable<Node> ChildNodesBreadthFirst(this Node node)
            => node.TraverseBreadthFirst(n => n.GetChildNodes());

        public static IEnumerable<Node> SelectChildNodesBreadthFirst(this IEnumerable<Node> nodes)
            => nodes.SelectMany(n => n.ChildNodesBreadthFirst());
        
        #endregion
    }
}
