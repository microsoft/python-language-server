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
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Types {
    /// <summary>
    /// Implements iterator type. Iterator type is used to supply information
    /// to the analysis on the return type of the 'next' method. 'Next' method
    /// is implemented manually via specialized function overload.
    /// </summary>
    internal sealed class PythonIteratorType : PythonType, IPythonIteratorType {
        private readonly PythonFunctionType _next;

        public PythonIteratorType(BuiltinTypeId typeId, IPythonModuleType declaringModule)
            : base("iterator", declaringModule, string.Empty, LocationInfo.Empty, typeId) {

            _next = new PythonFunctionType("next", declaringModule, this, string.Empty, LocationInfo.Empty);
            var overload = new PythonFunctionOverload("next", Array.Empty<IParameterInfo>(), LocationInfo.Empty);

            overload.SetReturnValueCallback(args => {
                if (args.Count > 0) {
                    if (args[0] is IPythonIterator iter) {
                        return iter.Next;
                    }
                    var t = args[0].GetPythonType<IPythonIteratorType>();
                    if (t != null && args[0] is IPythonFunction fn) {
                        return t.GetNext(fn.Self);
                    }
                }
                return DeclaringModule.Interpreter.GetBuiltinType(BuiltinTypeId.Unknown);
            });

            _next.AddOverload(overload);
        }
        public IMember GetNext(IPythonInstance instance) 
            => (instance as IPythonIterator)?.Next ?? DeclaringModule.Interpreter.GetBuiltinType(BuiltinTypeId.Unknown);

        public override IEnumerable<string> GetMemberNames() => Enumerable.Repeat(_next.Name, 1);
        public override IMember GetMember(string name) => name == _next.Name ? _next : null;
    }
}
