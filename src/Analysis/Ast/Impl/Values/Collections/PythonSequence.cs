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
            LocationInfo location,
            IReadOnlyList<object> contents = null
        ) : base(sequenceType, location) {
            if(contents != null) {
                if (contents.Count == 1 && contents[0] is IPythonSequence seq) {
                    Contents = seq.Contents;
                } else {
                    Contents = contents.OfType<IMember>().ToArray();
                }
            } else {
                Contents = Array.Empty<IMember>();
            }
        }

        /// <summary>
        /// Invokes indexer the instance.
        /// </summary>
        public override IMember Index(object index) {
            var n = GetIndex(index);
            if (n < 0) {
                n = Contents.Count + n; // -1 means last, etc.
            }
            if (n >= 0 && n < Contents.Count) {
                return Contents[n];
            }
            return Type.DeclaringModule.Interpreter.UnknownType;
        }

        public IReadOnlyList<IMember> Contents { get; }

        public virtual IPythonIterator GetIterator() => new PythonSequenceIterator(this);

        public static int GetIndex(object index) {
            switch (index) {
                case IPythonConstant c when c.Type.TypeId == BuiltinTypeId.Int || c.Type.TypeId == BuiltinTypeId.Long:
                    return (int)c.Value;
                case int i:
                    return i;
                case long l:
                    return (int)l;
                default:
                    // TODO: report bad index type.
                    return 0;
            }
        }
    }
}
