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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class PythonModuleType: IPythonType {
        public PythonModuleType(string name) {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        #region IPythonType
        public string Name { get; }
        public virtual string Documentation { get; }

        public virtual IPythonModule DeclaringModule => null;
        public BuiltinTypeId TypeId => BuiltinTypeId.Module;
        public bool IsBuiltIn => true;
        public bool IsTypeFactory => false;
        public IPythonFunction GetConstructors() => null;
        #endregion

        #region IMember
        public PythonMemberType MemberType => PythonMemberType.Module;
        #endregion

        #region IMemberContainer
        public virtual IMember GetMember(IModuleContext context, string name) => null;
        public virtual IEnumerable<string> GetMemberNames(IModuleContext moduleContext) => Enumerable.Empty<string>();
        #endregion
    }
}
