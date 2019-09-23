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

using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Modules {
    internal interface IAstNodeContainer {
        /// <summary>
        /// Provides access to AST nodes associated with objects such as
        /// <see cref="ClassDefinition"/> for <see cref="IPythonClassType"/>.
        /// Nodes are not available for library modules as AST is not retained
        /// in libraries in order to conserve memory.
        /// </summary>
        INode GetAstNode(object o);

        /// <summary>
        /// Associated AST node with the object.
        /// </summary>
        void AddAstNode(object o, INode n);

        void ClearContent();
    }
}
