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

using System;
using System.Collections.Generic;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.Analysis {
    /// <summary>
    /// Interface for providing an interpreter implementation for plugging into
    /// Python support for Visual Studio.
    /// 
    /// This interface provides information about Python types and modules,
    /// which will be used for program analysis and IntelliSense.
    /// 
    /// An interpreter is provided by an object implementing 
    /// <see cref="IPythonInterpreterFactory"/>.
    /// </summary>
    public interface IPythonInterpreter : IDisposable {
        /// <summary>
        /// Interpreter configuration.
        /// </summary>
        InterpreterConfiguration Configuration { get; }

        /// <summary>
        /// Python language version.
        /// </summary>
        PythonLanguageVersion LanguageVersion { get; }

        /// <summary>
        /// Path to the interpreter executable.
        /// </summary>
        string InterpreterPath { get; }

        /// <summary>
        /// Path to the interpreter lib folder.
        /// </summary>
        string LibraryPath { get; }

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
        /// Module resolution service.
        /// </summary>
        IModuleResolution ModuleResolution { get; }

        /// <summary>
        /// Application logger.
        /// </summary>
        ILogger Log { get; }

        /// <summary>
        /// Tells analyzer that module set has changed. Client application that tracks changes
        /// to the Python libraries (via watching file system or otherwise) should call this
        /// method in order to tell analyzer that modules were added or removed.
        /// </summary>
        void NotifyImportableModulesChanged();
    }
}
