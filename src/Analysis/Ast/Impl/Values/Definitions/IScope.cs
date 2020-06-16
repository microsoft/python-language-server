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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Values {
    /// <summary>
    /// Represents scope where variables can be declared.
    /// </summary>
    public interface IScope {
        /// <summary>
        /// Scope name. Typically name of the scope-defining <see cref="IScope.Node"/>
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Node defining the scope. Typically <see cref="ClassDefinition"/>
        /// or <see cref="FunctionDefinition"/>
        /// </summary>
        ScopeStatement Node { get; }

        /// <summary>
        /// Immediate parent of this scope.
        /// </summary>
        IScope OuterScope { get; }

        /// <summary>
        /// Module global scope.
        /// </summary>
        IGlobalScope GlobalScope { get; }

        /// <summary>
        /// Child scopes.
        /// </summary>
        IReadOnlyList<IScope> Children { get; }

        /// <summary>
        /// Enumerates scopes from this one to global scope.
        /// </summary>
        IEnumerable<IScope> EnumerateTowardsGlobal { get; }

        /// <summary>
        /// Enumerates scopes from global to this one.
        /// </summary>
        IEnumerable<IScope> EnumerateFromGlobal { get; }

        /// <summary>
        /// Collection of variables declared in the scope.
        /// </summary>
        IVariableCollection Variables { get; }

        /// <summary>
        /// Collection of variables declared via 'nonlocal' statement.
        /// </summary>
        IVariableCollection NonLocals { get; }

        /// <summary>
        /// Collection of global variables declared via 'global' statement.
        /// </summary>
        IVariableCollection Globals { get; }

        /// <summary>
        /// Collection of variables imported from other modules.
        /// </summary>
        IVariableCollection Imported { get; }

        /// <summary>
        /// Module the scope belongs to.
        /// </summary>
        IPythonModule Module { get; }

        /// <summary>
        /// Declares variable in the scope.
        /// </summary>
        /// <param name="name">Variable name.</param>
        /// <param name="value">Variable value.</param>
        /// <param name="source">Variable source.</param>
        /// <param name="location">Variable name node location.</param>
        void DeclareVariable(string name, IMember value, VariableSource source, Location location = default);

        /// <summary>
        /// Links variable from another module such as when it is imported.
        /// </summary>
        void LinkVariable(string name, IVariable v, Location location);
    }
}
