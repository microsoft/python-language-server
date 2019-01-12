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

        public override IMember Call(string memberName, IReadOnlyList<object> args) {
            var interpreter = _dictType.DeclaringModule.Interpreter;
            // Specializations
            switch (memberName) {
                case @"get":
                    return new PythonInstance(_dictType.ValueType);
                case @"items":
                    return new PythonInstance(TypingTypeFactory.CreateItemsViewType(interpreter, _dictType));
                case @"keys":
                    return new PythonInstance(TypingTypeFactory.CreateKeysViewType(interpreter, _dictType.KeyType));
                case @"values":
                    return new PythonInstance(TypingTypeFactory.CreateValuesViewType(interpreter, _dictType.ValueType));
                case @"iterkeys":
                    return new PythonInstance(TypingTypeFactory.CreateIteratorType(interpreter, _dictType.KeyType));
                case @"itervalues":
                    return new PythonInstance(TypingTypeFactory.CreateIteratorType(interpreter, _dictType.ValueType));
                case @"iteritems":
                    return new PythonInstance(TypingTypeFactory.CreateIteratorType(interpreter, _dictType.ItemType));
                case @"pop":
                    return new PythonInstance(_dictType.ValueType);
                case @"popitem":
                    return new PythonInstance(_dictType.ItemType);
            }
            return base.Call(memberName, args);
        }

        private IPythonCollection CreateList(IPythonType itemType, BuiltinTypeId typeId) {
            var listType = new TypingListType("List", BuiltinTypeId.List, itemType, _dictType.DeclaringModule.Interpreter, false);
            return new TypingList(listType);
        }
    }
}
