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

namespace Microsoft.Python.Analysis.Types {
    /// <summary>
    /// Describes callable entity.
    /// </summary>
    public interface IPythonCallable {
        /// <summary>
        /// Describes callable parameters.
        /// </summary>
        IReadOnlyList<IParameterInfo> Parameters { get; }

        /// <summary>
        /// Determines return value type given arguments.
        /// For annotated or stubbed functions the annotation
        /// type is always returned.
        /// </summary>
        IPythonType GetReturnType(IReadOnlyList<IPythonType> args = null);

        /// <summary>
        /// Return value documentation.
        /// </summary>
        string ReturnDocumentation { get; }
    }
}
