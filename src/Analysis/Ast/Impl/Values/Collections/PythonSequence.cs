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
using System.Collections.Generic;
using System.Linq;
using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis.Values {
    internal abstract class PythonSequence : PythonInstance, IPythonSequence {
        /// <summary>
        /// Creates sequence of the supplied type.
        /// </summary>
        /// <param name="sequenceType">Sequence type.</param>
        /// <param name="contents">Contents of the sequence (typically elements from the initialization).</param>
        /// <param name="location">Declaring location.</param>
        protected PythonSequence(
            IPythonSequenceType sequenceType,
            IEnumerable<IMember> contents,
            LocationInfo location = null
        ) : base(sequenceType, location) {
            Contents = contents?.ToArray() ?? Array.Empty<IMember>();
        }

        /// <summary>
        /// Retrieves value at a specific index.
        /// </summary>
        /// <returns>Element at the index or Unknown type if index is out of bounds.</returns>
        public virtual IMember GetValueAt(int index) {
            if (index < 0) {
                index = Contents.Count + index; // -1 means last, etc.
            }
            if (index >= 0 && index < Contents.Count) {
                return Contents[index];
            }
            return Type.DeclaringModule.Interpreter.UnknownType;
        }

        public IReadOnlyList<IMember> Contents { get; }

        public virtual IPythonIterator GetIterator() => new PythonSequenceIterator(this);
    }
}
