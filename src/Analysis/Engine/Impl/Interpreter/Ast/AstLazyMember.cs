// Python Tools for Visual Studio
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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Diagnostics;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstLazyMember<T> : ILazyMember {
        private readonly Func<T, IMember> _getter;
        private T _parameter;
        private IMember _value;

        [DebuggerStepThrough]
        public AstLazyMember(Func<T, IMember> getter, T parameter = default(T)) {
            _getter = getter;
            _parameter = parameter;
        }
        public PythonMemberType MemberType => PythonMemberType.Lazy;

        public string Name => "lazy";

        [DebuggerStepThrough]
        public IMember Get() => _value ?? (_value = _getter(_parameter));
    }
}
