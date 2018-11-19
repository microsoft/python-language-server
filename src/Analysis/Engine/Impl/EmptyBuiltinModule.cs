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

using System.Collections.Generic;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis {
    class EmptyBuiltinModule : IBuiltinPythonModule {
        private readonly string _name;

        public EmptyBuiltinModule(string name) {
            _name = name;
        }

        #region IBuiltinPythonModule Members

        public IMember GetAnyMember(string name) {
            return null;
        }

        #endregion

        #region IPythonModule Members

        public string Name {
            get { return _name; }
        }

        public IEnumerable<string> GetChildrenModules() {
            yield break;
        }

        public void Imported(IModuleContext context) {
        }

        public string Documentation {
            get { return string.Empty; }
        }

        #endregion

        #region IPythonType
        public IPythonModule DeclaringModule => null;
        public BuiltinTypeId TypeId => BuiltinTypeId.Module;
        public bool IsBuiltIn => true;
        public bool IsTypeFactory => false;
        #endregion

        #region IMemberContainer
        public IMember GetMember(IModuleContext context, string name) => null;
        public IEnumerable<string> GetMemberNames(IModuleContext moduleContext) {
            yield break;
        }
        #endregion

        #region IMember
        public PythonMemberType MemberType => PythonMemberType.Module;
        #endregion
    }
}
