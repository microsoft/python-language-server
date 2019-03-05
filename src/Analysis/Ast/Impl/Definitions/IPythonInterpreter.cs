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
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.Analysis {
    /// <summary>
    /// Describes Python interpreter associated with the analysis.
    /// </summary>
    public interface IPythonInterpreter  {
        /// <summary>
        /// Interpreter configuration.
        /// </summary>
        InterpreterConfiguration Configuration { get; }

        /// <summary>
        /// Python language version.
        /// </summary>
        PythonLanguageVersion LanguageVersion { get; }

        /// <summary>
        /// Gets a well known built-in type such as int, list, dict, etc...
        /// </summary>
        /// <param name="id">The built-in type to get</param>
        /// <returns>An IPythonType representing the type.</returns>
        /// <exception cref="KeyNotFoundException">
        /// The requested type cannot be resolved by this interpreter.
        /// </exception>
        IPythonType GetBuiltinType(BuiltinTypeId id);

        /// <summary>
        /// Unknown type.
        /// </summary>
        IPythonType UnknownType { get; }

        /// <summary>
        /// Regular module resolution service.
        /// </summary>
        IModuleManagement ModuleResolution { get; }

        /// <summary>
        /// Stub resolution service.
        /// </summary>
        IModuleResolution TypeshedResolution { get; }
    }
}
