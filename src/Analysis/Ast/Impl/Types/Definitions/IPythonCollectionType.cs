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

using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Types {
    /// <summary>
    /// Represents instance of a collection.
    /// </summary>
    public interface IPythonCollectionType : IPythonType {
        /// <summary>
        /// Type of the collection iterator.
        /// </summary>
        IPythonIteratorType IteratorType { get; }

        /// <summary>
        /// Retrieves iterator from an instance.
        /// </summary>
        IPythonIterator GetIterator(IPythonInstance instance);

        /// <summary>
        /// Indicates if the collection is mutable (such as list) or not (such as tuple).
        /// </summary>
        bool IsMutable { get; }
    }
}
