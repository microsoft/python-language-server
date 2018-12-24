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
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Types {
    internal sealed class PythonFunctionOverload : IPythonFunctionOverload, ILocatedMember {
        // Allow dynamic function specialization, such as defining return types for builtin
        // functions that are impossible to scrape and that are missing from stubs.
        private Func<IReadOnlyList<IMember>, IMember> _returnValueCallback;
        // Return value can be an instance or a type info. Consider type(C()) returning
        // type info of C vs. return C() that returns an instance of C.
        private IMember _returnValue;

        public PythonFunctionOverload(
            string name,
            IEnumerable<IParameterInfo> parameters,
            LocationInfo loc,
            string returnDocumentation = null
        ) {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Parameters = parameters?.ToArray() ?? throw new ArgumentNullException(nameof(parameters));
            Location = loc ?? LocationInfo.Empty;
            ReturnDocumentation = returnDocumentation;
        }

        internal void SetDocumentation(string doc) {
            if (Documentation != null) {
                throw new InvalidOperationException("cannot set Documentation twice");
            }
            Documentation = doc;
        }

        internal void AddReturnValue(IMember value) {
            if (_returnValue.IsUnknown()) {
                SetReturnValue(value);
                return;
            } 
            var type = PythonUnion.Combine(_returnValue.GetPythonType(), value.GetPythonType());
            // Track instance vs type info.
            _returnValue = value is IPythonInstance ? new PythonInstance(type) : (IMember)type;
        }

        internal void SetReturnValue(IMember value) => _returnValue = value;

        internal void SetReturnValueCallback(Func<IReadOnlyList<IMember>, IMember> returnValueCallback)
            => _returnValueCallback = returnValueCallback;

        public string Name { get; }
        public string Documentation { get; private set; }
        public string ReturnDocumentation { get; }
        public IReadOnlyList<IParameterInfo> Parameters { get; }
        public LocationInfo Location { get; }
        public PythonMemberType MemberType => PythonMemberType.Function;

        public IMember GetReturnValue(IPythonInstance instance, IReadOnlyList<IMember> args) {
            // First try supplied specialization callback.
            var rt = _returnValueCallback?.Invoke(args);
            if (!rt.IsUnknown()) {
                return rt;
            }

            // Then see if return value matches type of one of the input arguments.
            var t = _returnValue.GetPythonType();
            if (!(t is IPythonCallableArgumentType) && !t.IsUnknown()) {
                return _returnValue;
            }

            if (t is IPythonCallableArgumentType cat && args != null) {
                rt = cat.ParameterIndex < args.Count ? args[cat.ParameterIndex] : null;
                if (!rt.IsUnknown()) {
                    return rt;
                }
            }
            return _returnValue;
        }
    }
}
