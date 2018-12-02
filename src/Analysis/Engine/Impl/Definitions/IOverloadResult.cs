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

namespace Microsoft.PythonTools.Analysis {
    public interface IOverloadResult {
        /// <summary>
        /// Function name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Function documentation.
        /// </summary>
        string Documentation { get; }

        /// <summary>
        /// First parameter if removed from the set.
        /// Typically 'self' or 'cls'.
        /// </summary>
        ParameterResult FirstParameter { get; }

        /// <summary>
        /// Function parameters. First parameter may be removed, in which case
        /// it is present as <see cref="FirstParameter"/>.
        /// </summary>
        ParameterResult[] Parameters { get; }

        /// <summary>
        /// Possible return types.
        /// </summary>
        IReadOnlyList<string> ReturnType { get; }
    }
}
