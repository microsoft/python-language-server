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
using System.Linq;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Types.Collections {
    /// <summary>
    /// Implements iterator type. Iterator type is used to supply information
    /// to the analysis on the return type of the 'next' method. 'Next' method
    /// is implemented manually via specialized function overload.
    /// </summary>
    internal sealed class PythonIteratorType : PythonTypeWrapper, IPythonIteratorType {
        /// <summary>
        /// Creates type info for an iterator.
        /// </summary>
        /// <param name="typeId">Iterator type id, such as <see cref="BuiltinTypeId.StrIterator"/>.</param>
        /// <param name="declaringModule">Declaring module</param>
        public PythonIteratorType(BuiltinTypeId typeId, IPythonModule declaringModule) : base(typeId, declaringModule) { }

        public IMember GetNext(IPythonInstance instance)
            => (instance as IPythonIterator)?.Next ?? UnknownType;
    }
}
