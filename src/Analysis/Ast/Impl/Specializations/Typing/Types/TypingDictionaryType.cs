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

using Microsoft.Python.Analysis.Specializations.Typing.Values;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Types.Collections;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Specializations.Typing.Types {
    /// <summary>
    /// Represents type info of typing.Dict[TK, TV]
    /// </summary>
    internal class TypingDictionaryType : PythonDictionaryType, ITypingDictionaryType {
        private IPythonType _itemType;

        /// <summary>
        /// Creates type info of typing.Dict[TK, TV]
        /// </summary>
        /// <param name="name">Type name (Dict, Mapping, ...)</param>
        /// <param name="keyType">Type of dictionary keys.</param>
        /// <param name="valueType">Type of dictionary values.</param>
        /// <param name="interpreter">Python interpreter.</param>
        /// <param name="isMutable">Tells if collection is mutable (Dict) or not (Mapping)</param>
        public TypingDictionaryType(string name, IPythonType keyType, IPythonType valueType, IPythonInterpreter interpreter, bool isMutable)
            : base(interpreter.ModuleResolution.GetSpecializedModule("typing"), isMutable) {
            KeyType = keyType;
            ValueType = valueType;
            Name = $"{name}[{keyType.Name}, {valueType.Name}]";
            QualifiedName = $"typing:{name}[{keyType.QualifiedName}, {valueType.QualifiedName}]";
        }

        public IPythonType KeyType { get; }
        public IPythonType ValueType { get; }
        public IPythonType ItemType => _itemType ?? (_itemType = CreateItemType());

        public override string Name { get; }
        public override string QualifiedName { get; }

        public override IPythonInstance CreateInstance(IArgumentSet args) => new TypingDictionary(this);
        public override IMember Index(IPythonInstance instance, IArgumentSet args) => ValueType.CreateInstance(args);
        public override bool IsSpecialized => true;

        private TypingTupleType CreateItemType() {
            var itemType = new TypingTupleType(new[] { KeyType, ValueType }, DeclaringModule, DeclaringModule.Interpreter);
            return itemType;
        }

        public override bool Equals(object obj) {
            if (!(obj is TypingDictionaryType other)) {
                return false;
            }

            if ((KeyType.IsGenericParameter() && ValueType.IsGenericParameter()) || 
                (other.KeyType.IsGenericParameter() && other.ValueType.IsGenericParameter())) {
                // Generic match - i.e. Mapping[K, V] matches Dict[int, str].
                return true;
            }
            return PythonTypeComparer.Instance.Equals(KeyType, other.KeyType) && 
                   PythonTypeComparer.Instance.Equals(ValueType, other.ValueType);
        }

        public override int GetHashCode() => KeyType.GetHashCode() ^ ValueType.GetHashCode() ^ Name.GetHashCode();
    }
}
