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
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Types {
    /// <summary>
    /// Provides the location of a member. This should be implemented on a class
    /// which also implements <see cref="IPythonType" /> or <see cref="IPythonInstance" />.
    /// </summary>
    public interface ILocatedMember: IMember {
        /// <summary>
        /// Module that defines the member.
        /// </summary>
        IPythonModule DeclaringModule { get; }

        /// <summary>
        /// Location where the member is defined.
        /// </summary>
        LocationInfo Definition { get; }

        /// <summary>
        /// AST node where the member is defined. For example, <see cref="ClassDefinition"/>
        /// for a class, <see cref="FunctionDefinition"/> for functions, methods and properties
        /// or a <see cref="NameExpression"/> for variables.
        /// </summary>
        Node DefinitionNode { get; }

        /// <summary>
        /// List of references to the member.
        /// </summary>
        IReadOnlyList<LocationInfo> References { get; }

        /// <summary>
        /// Add member reference.
        /// </summary>
        void AddReference(IPythonModule module, Node location);
    }
}
