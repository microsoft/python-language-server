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
    /// Represents generic type, such as class or function.
    /// Generic type is a template for the actual type.
    /// </summary>
    public interface IGenericType : IPythonTemplateType {
        /// <summary>
        /// Type parameters such as in Tuple[T1, T2. ...] or
        /// Generic[_T1, _T2, ...] as returned by TypeVar.
        /// </summary>
        IReadOnlyList<IGenericTypeDefinition> Parameters { get; }

        IPythonType CreateSpecificType(IReadOnlyList<IPythonType> typeArguments, IPythonModule declaringModule, LocationInfo location = null);
    }
}
