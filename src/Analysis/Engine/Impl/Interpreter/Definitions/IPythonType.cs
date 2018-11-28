// Python Tools for Visual Studio
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
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Interpreter {
    public interface IPythonType : IMemberContainer, IMember {
        // Python __name__.
        string Name { get; }

        /// <summary>
        /// Module the type is declared in.
        /// </summary>
        IPythonModule DeclaringModule { get; }

        /// <summary>
        /// Indicates built-in type id such as 'int' or 'str'
        /// or 'type' for user-defined entities.
        /// </summary>
        BuiltinTypeId TypeId { get; }

        /// <summary>
        /// Human-readable documentation that may be displayed in the editor hover tooltip.
        /// </summary>
        string Documentation { get; }

        /// <summary>
        /// Indicates if type is a built-in type.
        /// </summary>
        bool IsBuiltIn { get; }

        /// <summary>
        /// Type is a type class factory.
        /// </summary>
        bool IsTypeFactory { get; }

        /// <summary>
        /// Returns constructors of the type, if any.
        /// </summary>
        /// <returns></returns>
        IPythonFunction GetConstructors();
    }
}
