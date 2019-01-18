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
using Microsoft.Python.Analysis.Types.Collections;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Analysis.Values.Collections;

namespace Microsoft.Python.Analysis.Specializations {
    public static class BuiltinsSpecializations {
        public static ReturnValueProvider Identity
            => (module, overload, location, args) => args.Count > 0 ? args[0] : null;

        public static ReturnValueProvider TypeInfo
            => (module, overload, location, args) => args.Count > 0 ? args[0].GetPythonType() : null;

        public static IMember Iterator(IPythonModule module, IPythonFunctionOverload overload, LocationInfo location, IReadOnlyList<IMember> args) {
            if (args.Count > 0) {
                if (args[0] is IPythonCollection seq) {
                    return seq.GetIterator();
                }
                var t = args[0].GetPythonType();
                if (t.IsBuiltin && t.Name == "str") {
                    return new PythonTypeIterator(BuiltinTypeId.StrIterator, BuiltinTypeId.Str, module.Interpreter);
                }
            }
            return null;
        }

        public static IMember List(IPythonInterpreter interpreter, IPythonFunctionOverload overload, LocationInfo location, IReadOnlyList<IMember> args)
            => PythonCollectionType.CreateList(interpreter, location, args);

        public static IMember ListOfStrings(IPythonModule module, IPythonFunctionOverload overload, LocationInfo location, IReadOnlyList<IMember> args) {
            var type = new TypingListType("List", module.Interpreter.GetBuiltinType(BuiltinTypeId.Str), module.Interpreter, false);
            return new TypingList(type, location);
        }
        public static IMember DictStringToObject(IPythonModule module, IPythonFunctionOverload overload, LocationInfo location, IReadOnlyList<IMember> args) {
            var str = module.Interpreter.GetBuiltinType(BuiltinTypeId.Str);
            var obj = module.Interpreter.GetBuiltinType(BuiltinTypeId.Object);
            var type = new TypingDictionaryType("Dict", str, obj, module.Interpreter, false);
            return new TypingDictionary(type, location);
        }

        public static ReturnValueProvider Next
                => (module, overload, location, args) => args.Count > 0 && args[0] is IPythonIterator it ? it.Next : null;

        public static IMember __iter__(IPythonInterpreter interpreter, BuiltinTypeId contentTypeId) {
            var fn = new PythonFunctionType(@"__iter__", interpreter.ModuleResolution.BuiltinsModule, null, string.Empty, LocationInfo.Empty);
            var o = new PythonFunctionOverload(fn.Name, interpreter.ModuleResolution.BuiltinsModule, _ => fn.Location);
            o.AddReturnValue(PythonTypeIterator.FromTypeId(interpreter, contentTypeId));
            fn.AddOverload(o);
            return fn;
        }

        public static IMember Range(IPythonModule module, IPythonFunctionOverload overload, LocationInfo location, IReadOnlyList<IMember> args) {
            if (args.Count > 0) {
                var type = new PythonCollectionType(null, BuiltinTypeId.List, module.Interpreter, false);
                return new PythonCollection(type, location, new [] {args[0]});
            }
            return null;
        }

        public static ReturnValueProvider CollectionItem
            => (module, overload, location, args) => args.Count > 0 && args[0] is PythonCollection c ? c.Contents.FirstOrDefault() : null;
    }
}
