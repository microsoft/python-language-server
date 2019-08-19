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
using System.Linq;
using Microsoft.Python.Analysis.Specializations.Typing.Types;
using Microsoft.Python.Analysis.Specializations.Typing.Values;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Utilities;
using Microsoft.Python.Core.Text;

namespace Microsoft.Python.Analysis.Specializations.Typing {
    internal static class TypingTypeFactory {
        public static ITypingListType CreateListType(IPythonInterpreter interpreter, string typeName, BuiltinTypeId typeId, IPythonType itemType, bool isMutable)
            => new TypingListType(typeName, typeId, itemType, interpreter, isMutable);

        public static ITypingTupleType CreateTupleType(IPythonInterpreter interpreter, IReadOnlyList<IPythonType> types)
            => new TypingTupleType(types, null, interpreter);

        public static ITypingIteratorType CreateIteratorType(IPythonInterpreter interpreter, IPythonType itemType)
            => new TypingIteratorType(itemType, BuiltinTypeId.ListIterator, interpreter);

        public static ITypingDictionaryType CreateDictionary(IPythonInterpreter interpreter, string typeName, IPythonType keyType, IPythonType valueType, bool isMutable)
            => new TypingDictionaryType(typeName, keyType, valueType, interpreter, isMutable);

        public static ITypingListType CreateKeysViewType(IPythonInterpreter interpreter, IPythonType keyType)
            => new TypingListType("KeysView", BuiltinTypeId.DictKeys, keyType, interpreter, false);

        public static ITypingListType CreateValuesViewType(IPythonInterpreter interpreter, IPythonType valueType)
            => new TypingListType("ValuesView", BuiltinTypeId.DictKeys, valueType, interpreter, false);

        public static ITypingListType CreateItemsViewType(IPythonInterpreter interpreter, ITypingDictionaryType dict) {
            var typeName = CodeFormatter.FormatSequence("ItemsView", '[', new[] { dict.KeyType, dict.ValueType });
            return new TypingListType(typeName, BuiltinTypeId.DictItems, dict.ItemType, interpreter, false, false);
        }

        public static ITypingListType CreateItemsViewType(IPythonInterpreter interpreter, IPythonType keyType, IPythonType valueType) {
            var types = new[] {keyType, valueType};
            var typeName = CodeFormatter.FormatSequence("ItemsView", '[', types);
            var itemType = CreateTupleType(interpreter, types);
            return new TypingListType(typeName, BuiltinTypeId.DictItems, itemType, interpreter, false, false);
        }

        public static IPythonType CreateUnionType(IPythonInterpreter interpreter, IReadOnlyList<IMember> types, IPythonModule declaringModule)
            => new PythonUnionType(types.Select(a => a.GetPythonType()), declaringModule);

        public static ITypingNamedTupleType CreateNamedTupleType(string tupleName, IReadOnlyList<string> itemNames, IReadOnlyList<IPythonType> itemTypes, IPythonModule declaringModule, IndexSpan indexSpan)
            => new NamedTupleType(tupleName, itemNames, itemTypes, declaringModule, indexSpan);

        public static IPythonType CreateType(IPythonModule declaringModule, IPythonType type)
            => new TypingType(declaringModule, type);
    }
}
