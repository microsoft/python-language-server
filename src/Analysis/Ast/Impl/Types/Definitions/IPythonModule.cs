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
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Types {
    /// <summary>
    /// Represents a Python module.
    /// </summary>
    public interface IPythonModule : IPythonType {
        /// <summary>
        /// File path to the module.
        /// </summary>
        string FilePath { get; }

        /// <summary>
        /// Module URI.
        /// </summary>
        Uri Uri { get; }

        /// <summary>
        /// Module analysis.
        /// </summary>
        IDocumentAnalysis Analysis { get; }

        /// <summary>
        /// Interpreter associated with the module.
        /// </summary>
        IPythonInterpreter Interpreter { get; }

        /// <summary>
        /// Module type (user, library, stub).
        /// </summary>
        ModuleType ModuleType { get; }

        /// <summary>
        /// Module stub, if any.
        /// </summary>
        IPythonModule Stub { get; }

        /// <summary>
        /// Global cope of the module.
        /// </summary>
        IGlobalScope GlobalScope { get; }
        
        /// <summary>
        /// If module is a stub points to the primary module.
        /// Typically used in code navigation scenarios when user
        /// wants to see library code and not a stub.
        /// </summary>
        IPythonModule PrimaryModule { get; }

        /// <summary>
        /// Indicates if module is restored from database.
        /// </summary>
        bool IsPersistent { get; }
    }
}
