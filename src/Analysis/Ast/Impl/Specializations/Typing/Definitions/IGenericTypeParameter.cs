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
using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis.Specializations.Typing {
    /// <summary>
    /// Represents generic type parameter. Typically value returned by TypeVar.
    /// </summary>
    public interface IGenericTypeParameter: IPythonType {
        /// <summary>
        /// List of constraints for the type.
        /// </summary>
        /// <remarks>See 'https://docs.python.org/3/library/typing.html#typing.TypeVar'</remarks>
        IReadOnlyList<IPythonType> Constraints { get; }
    }
}
