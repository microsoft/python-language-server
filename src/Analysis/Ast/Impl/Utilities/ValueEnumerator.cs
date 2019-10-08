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

using System.Linq;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Collections;

namespace Microsoft.Python.Analysis.Utilities {
    internal sealed class ValueEnumerator {
        private readonly IMember _value;
        private readonly IPythonType _unknown;
        private readonly IPythonModule _declaringModule;
        private readonly ImmutableArray<IMember> _values;
        private readonly ImmutableArray<ValueEnumerator> _nested;
        private int _index;

        /// <summary>
        /// Constructs an enumerator over the values of the given type
        /// </summary>
        /// <param name="value">Collection to iterate over</param>
        /// <param name="unknown">Default type when we cannot find type from collection</param>
        public ValueEnumerator(IMember value, IPythonType unknown, IPythonModule declaringModule) {
            _value = value;
            _unknown = unknown;
            _declaringModule = declaringModule;

            if (value.GetPythonType() is IPythonUnionType union) {
                _nested = union.Select(v => new ValueEnumerator(v.CreateInstance(ArgumentSet.WithoutContext), unknown, declaringModule)).ToImmutableArray();
                return;
            }

            switch (value) {
                // Tuple = 'tuple value' (such as from callable). Transfer values.
                case IPythonCollection seq:
                    _values = seq.Contents.ToImmutableArray();
                    break;
                // Create singleton list of value when cannot identify tuple
                default:
                    _values = ImmutableArray<IMember>.Create(value);
                    break;
            }
        }

        public IMember Next() {
            IMember t = Peek;
            _index++;

            foreach (var ve in _nested) {
                ve.Next();
            }

            return t;
        }

        public IMember Peek {
            get {
                if (_nested.Count > 0) {
                    return new PythonUnionType(_nested.Select(ve => ve.Peek.GetPythonType()), _declaringModule).CreateInstance(ArgumentSet.WithoutContext);
                }

                if (_values.Count > 0) {
                    return _index < _values.Count ? _values[_index] : _values[_values.Count - 1];
                }

                return Filler.CreateInstance(ArgumentSet.WithoutContext);
            }
        }

        private IPythonType Filler {
            get {
                switch (_value?.GetPythonType()) {
                    case ITypingListType tlt:
                        return tlt.ItemType;
                    case ITypingTupleType tt when tt.ItemTypes?.Count > 0:
                        var itemTypes = tt.ItemTypes;
                        if (itemTypes.Count > 0) {
                            // If mismatch between enumerator index and actual types, duplicate last type
                            return _index < itemTypes.Count ? itemTypes[_index] : itemTypes[itemTypes.Count - 1];
                        }
                        break;
                }
                return _unknown;
            }
        }
    }
}
