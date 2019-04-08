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
using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis.Values.Collections {
    internal class PythonCollection : PythonInstance, IPythonCollection {
        /// <summary>
        /// Creates collection of the supplied types.
        /// </summary>
        /// <param name="collectionType">Collection type.</param>
        /// <param name="contents">Contents of the collection (typically elements from the initialization).</param>
        /// <param name="flatten">If true and contents is a single element</param>
        /// <param name="exact">True if the contents are an exact representation of the collection contents.</param>
        /// and is a sequence, the sequence elements are copied rather than creating
        /// a sequence of sequences with a single element.</param>
        public PythonCollection(
            IPythonType collectionType,
            IReadOnlyList<IMember> contents,
            bool flatten = true,
            bool exact = false
        ) : base(collectionType) {
            var c = contents ?? Array.Empty<IMember>();
            if (flatten && c.Count == 1 && c[0] is IPythonCollection seq) {
                Contents = seq.Contents;
            } else {
                Contents = c;
            }
            IsExact = exact;
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

        public IReadOnlyList<IMember> Contents { get; protected set; }
        public override IPythonIterator GetIterator() => new PythonIterator(BuiltinTypeId.ListIterator, this);

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

        public bool IsExact { get; }
    }
}
