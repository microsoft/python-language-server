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
using Microsoft.Python.Analysis.Types.Collections;

namespace Microsoft.Python.Analysis.Values.Collections {
    /// <summary>
    /// Default mutable list with mixed content.
    /// </summary>
    internal class PythonList : PythonSequence {
        /// <summary>
        /// Creates list of the supplied type.
        /// </summary>
        /// <param name="listType">List type.</param>
        /// <param name="contents">Contents of the sequence (typically elements from the initialization).</param>
        /// <param name="location">Declaring location.</param>
        /// <param name="flatten">If true and contents is a single element
        /// and is a sequence, the sequence elements are copied rather than creating
        /// a sequence of sequences with a single element.</param>
        public PythonList(PythonListType listType, LocationInfo location, IEnumerable<IMember> contents, bool flatten = true) :
            base(listType, location, contents, flatten) { }

        /// <summary>
        /// Creates list. List type is determined fro the interpreter.
        /// </summary>
        /// <param name="interpreter">Python interpreter.</param>
        /// <param name="contents">Contents of the sequence (typically elements from the initialization).</param>
        /// <param name="location">Declaring location.</param>
        /// <param name="flatten">If true and contents is a single element
        /// and is a sequence, the sequence elements are copied rather than creating
        /// a sequence of sequences with a single element.</param>
        public PythonList(IPythonInterpreter interpreter, LocationInfo location, IEnumerable<IMember> contents, bool flatten = true) :
            this(SequenceTypeCache.GetType<PythonListType>(interpreter), location, contents, flatten) { }
    }
}
