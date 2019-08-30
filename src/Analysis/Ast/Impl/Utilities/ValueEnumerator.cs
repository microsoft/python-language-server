using System.Linq;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Utilities {
    internal sealed class ValueEnumerator {
        private readonly IMember _value;
        private readonly IMember _unknown;
        private readonly IMember[] _values;
        private int _index;

        /// <summary>
        /// Constructs an enumerator over the values of the given type
        /// </summary>
        /// <param name="value">Collection to iterate over</param>
        /// <param name="unknown">Default type when we cannot find type from collection</param>
        public ValueEnumerator(IMember value, IMember unknown) {
            _value = value;
            _unknown = unknown;
            switch (value) {
                // Tuple = 'tuple value' (such as from callable). Transfer values.
                case IPythonCollection seq:
                    _values = seq.Contents.ToArray();
                    break;
                // Create singleton list of value when cannot identify tuple
                default:
                    _values = new[] { value };
                    break;
            }
        }

        public IMember Next {
            get {
                IMember t = Peek;
                _index++;
                return t;
            }
        }

        public IMember Peek {
            get {
                if (_values.Length > 0) {
                    return _index < _values.Length ? _values[_index] : _values[_values.Length - 1];
                } else {
                    return Filler;
                }
            }
        }

        private IMember Filler {
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
