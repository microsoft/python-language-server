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
using Microsoft.Python.Analysis.Specializations.Typing.Types;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Analysis.Values.Collections;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Specializations.Typing.Values {
    /// <summary>
    /// Represents instance of typing.Dict[TK, TV]
    /// </summary>
    internal class TypingDictionary : PythonDictionary {
        private readonly TypingDictionaryType _dictType;
        
        public TypingDictionary(TypingDictionaryType dictType, LocationInfo location = null)
            : base(dictType, location ?? LocationInfo.Empty, EmptyDictionary<IMember, IMember>.Instance) {
            _dictType = dictType;
        }

        public override IPythonIterator GetIterator() {
            var iteratorTypeId = _dictType.TypeId.GetIteratorTypeId();
            var iteratorType = new TypingIteratorType(_dictType.ItemType, iteratorTypeId, Type.DeclaringModule.Interpreter);
            return new TypingIterator(iteratorType, this);
        }

        public override IMember Index(object key) => new PythonInstance(_dictType.ValueType);

        public override IMember Call(string memberName, IArgumentSet args) {
            // Specializations
            switch (memberName) {
                case @"get":
                    return new PythonInstance(_dictType.ValueType);
                case @"items":
                    return GetItems();
                case @"keys":
                    return GetKeys();
                case @"values":
                    return GetValues();
                case @"iterkeys":
                    return GetKeys().GetIterator();
                case @"itervalues":
                    return GetValues().GetIterator();
                case @"iteritems":
                    return GetItems().GetIterator();
                case @"pop":
                    return new PythonInstance(_dictType.ValueType);
                case @"popitem":
                    return new PythonInstance(_dictType.ItemType);
            }
            return base.Call(memberName, args);
        }

        private TypingList _keys;
        private TypingList GetKeys()
            => _keys ?? (_keys = new TypingList(TypingTypeFactory.CreateKeysViewType(_dictType.DeclaringModule.Interpreter, _dictType.KeyType)));

        private TypingList _values;
        private TypingList GetValues()
            => _values ?? (_values = new TypingList(TypingTypeFactory.CreateValuesViewType(_dictType.DeclaringModule.Interpreter, _dictType.ValueType)));

        private TypingList _items;
        private TypingList GetItems()
            =>_items ?? (_items = new TypingList(TypingTypeFactory.CreateItemsViewType(_dictType.DeclaringModule.Interpreter, _dictType)));
    }
}
