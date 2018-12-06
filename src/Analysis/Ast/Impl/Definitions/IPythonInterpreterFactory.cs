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

using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Core.Logging;

namespace Microsoft.Python.Analysis {
    /// <summary>
    /// Provides a factory for creating IPythonInterpreters for a specific
    /// Python implementation.
    /// 
    /// The factory includes information about what type of interpreter will be
    /// created - this is used for displaying information to the user and for
    /// tracking per-interpreter settings.
    /// 
    /// It also contains a method for creating an interpreter. This allows for
    /// stateful interpreters that participate in analysis or track other state.
    /// </summary>
    public interface IPythonInterpreterFactory {
        /// <summary>
        /// Configuration settings for the interpreter.
        /// </summary>
        InterpreterConfiguration Configuration { get; }

        /// <summary>
        /// Application logger.
        /// </summary>
        ILogger Log { get; }

        /// <summary>
        /// Cache search path.
        /// </summary>
        string SearchPathCachePath { get; }

        /// <summary>
        /// Module information database path.
        /// </summary>
        string DatabasePath { get; }

        /// <summary>
        /// Indicates that analysis is using default database.
        /// </summary>
        bool UseDefaultDatabase { get; }

        /// <summary>
        /// Creates an IPythonInterpreter instance.
        /// </summary>
        IPythonInterpreter CreateInterpreter();
    }
}
