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
                    return new PythonTypeIterator(t.DeclaringModule, BuiltinTypeId.StrIterator, BuiltinTypeId.Str);
                }
            }
            return null;
        }

        public static IMember List(IPythonModule module, IPythonFunctionOverload overload, LocationInfo location, IReadOnlyList<IMember> args)
            => PythonCollectionType.CreateList(module, location, args);

        public static IMember ListOfStrings(IPythonModule module, IPythonFunctionOverload overload, LocationInfo location, IReadOnlyList<IMember> args)
            => new TypingList(new TypingListType(module, module.Interpreter.GetBuiltinType(BuiltinTypeId.Str)), location);

        //public static IMember Dict(IPythonModule module, IPythonFunctionOverload overload, LocationInfo location, IReadOnlyList<IMember> args)
        //    => new PythonDictionary(module.Interpreter, location, args);

        public static ReturnValueProvider Next
                => (module, overload, location, args) => args.Count > 0 && args[0] is IPythonIterator it ? it.Next : null;

        public static IMember __iter__(IPythonModule declaringModule, BuiltinTypeId contentTypeId) {
            var fn = new PythonFunctionType(@"__iter__", declaringModule, null, string.Empty, LocationInfo.Empty);
            var o = new PythonFunctionOverload(fn.Name, declaringModule, _ => fn.Location);
            o.AddReturnValue(PythonTypeIterator.FromTypeId(declaringModule, contentTypeId));
            fn.AddOverload(o);
            return fn;
        }
    }
}
