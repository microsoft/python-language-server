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
    /// Represents a function.
    /// </summary>
    public interface IPythonFunction : IPythonClassMember {
        /// <summary>
        /// Function definition in the AST.
        /// </summary>
        FunctionDefinition FunctionDefinition { get; }

        bool IsStatic { get; }
        bool IsClassMethod { get; }

        /// <summary>
        /// List of function overloads
        /// </summary>
        IReadOnlyList<IPythonFunctionOverload> Overloads { get; }
    }
}
