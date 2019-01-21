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
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Types {
    /// <summary>
    /// Represents a single overload of a function.
    /// </summary>
    public interface IPythonFunctionOverload {
        /// <summary>
        /// The corresponding function definition node.
        /// </summary>
        FunctionDefinition FunctionDefinition { get; }

        /// <summary>
        /// he corresponding function or property.
        /// </summary>
        IPythonClassMember ClassMember { get; }

        /// <summary>
        /// Function name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Overload documentation.
        /// </summary>
        string Documentation { get; }
        
        /// <summary>
        /// Overload parameters.
        /// </summary>
        IReadOnlyList<IParameterInfo> Parameters { get; }

        /// <summary>
        /// Determines return value type given arguments for the particular call.
        /// For annotated or stubbed functions the annotation type is always returned.
        /// </summary>
        IMember GetReturnValue(LocationInfo callLocation, IArgumentSet args);

        /// <summary>
        /// Return value documentation.
        /// </summary>
        string ReturnDocumentation { get; }

        /// <summary>
        /// Function definition is decorated with @overload.
        /// </summary>
        bool IsOverload { get; }
    }
}
