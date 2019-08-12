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

using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Types {
    /// <summary>
    /// Represents a built-in property which has a getter/setter.  
    /// </summary>
    public interface IPythonPropertyType : IPythonClassMember {
        /// <summary>
        /// Function definition in the AST.
        /// </summary>
        FunctionDefinition FunctionDefinition { get; }

        /// <summary>
        /// A user readable description of the property.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Property is a @classmethod.
        /// </summary>
        bool IsClassMethod { get; }

        /// <summary>
        /// Property is @staticmethod.
        /// </summary>
        bool IsStatic { get; }

        /// <summary>
        /// True if the property is read-only.
        /// </summary>
        bool IsReadOnly { get; }
    }
}
