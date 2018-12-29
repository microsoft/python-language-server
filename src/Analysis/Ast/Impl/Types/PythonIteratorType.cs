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
        private static readonly string[] _methodNames = new[] { "next", "__next__" };
        private readonly PythonFunctionType[] _methods = new PythonFunctionType[2];

        /// <summary>
        /// Creates type info for an iterator.
        /// </summary>
        /// <param name="typeId">Iterator type id, such as <see cref="BuiltinTypeId.StrIterator"/>.</param>
        /// <param name="declaringModule">Declaring module</param>
        public PythonIteratorType(BuiltinTypeId typeId, IPythonModule declaringModule)
            : base("iterator", declaringModule, string.Empty, LocationInfo.Empty, typeId) {

            // Create 'next' members.
            _methods[0] = new PythonFunctionType(_methodNames[0], declaringModule, this, string.Empty, LocationInfo.Empty);
            _methods[1] = new PythonFunctionType(_methodNames[1], declaringModule, this, string.Empty, LocationInfo.Empty);

            // Both members share the same overload.
            var overload = new PythonFunctionOverload("next", Array.Empty<IParameterInfo>(), LocationInfo.Empty);

            // Set up the overload return type handler.
            overload.SetReturnValueProvider(args => {
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

            foreach (var m in _methods) {
                m.AddOverload(overload);
            }
        }
        public IMember GetNext(IPythonInstance instance)
            => (instance as IPythonIterator)?.Next ?? DeclaringModule.Interpreter.GetBuiltinType(BuiltinTypeId.Unknown);

        public override IEnumerable<string> GetMemberNames() => _methodNames;
        public override IMember GetMember(string name) => _methods.FirstOrDefault(m => m.Name == name);
    }
}
