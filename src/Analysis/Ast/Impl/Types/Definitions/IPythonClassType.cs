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
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Types {
    /// <summary>
    /// Represents Python class type definition.
    /// </summary>
    public interface IPythonClassType : IPythonClassMember, IGenericType {
        /// <summary>
        /// Class definition node in the AST.
        /// </summary>
        ClassDefinition ClassDefinition { get; }

        /// <summary>
        /// Python Method Resolution Order (MRO).
        /// </summary>
        IReadOnlyList<IPythonType> Mro { get; }

        /// <summary>
        /// Base types.
        /// </summary>
        IReadOnlyList<IPythonType> Bases { get; }

        /// <summary>
        /// If class is created off generic template, represents
        /// pairs of the generic parameter / actual supplied type.
        /// </summary>
        IReadOnlyDictionary<string, IPythonType> GenericParameters { get; }
    }
}
