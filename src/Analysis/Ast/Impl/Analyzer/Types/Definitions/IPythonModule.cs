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
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Python.Analysis {
    /// <summary>
    /// Represents a Python module.
    /// </summary>
    public interface IPythonModule : IPythonType, IPythonFile, ILocatedMember {
        /// <summary>
        /// Interpreter associated with the module.
        /// </summary>
        IPythonInterpreter Interpreter { get; }

        /// <summary>
        /// Modules imported by this module.
        /// </summary>
        /// <returns></returns>
        IEnumerable<string> GetChildrenModuleNames();

        /// <summary>
        /// Ensures that module content is loaded and analysis has completed.
        /// Typically module content is loaded at the creation time, but delay
        /// loaded (lazy) modules may choose to defer content retrieval and
        /// analysis until later time, when module members are actually needed.
        /// </summary>
        Task LoadAndAnalyzeAsync(CancellationToken cancellationToken = default);
    }
}
