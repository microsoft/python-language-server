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

using System.Collections.Generic;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Ast;

namespace Microsoft.PythonTools.Analysis {
    class EmptyBuiltinModule : PythonModuleType, IBuiltinPythonModule {
        public EmptyBuiltinModule(string name): base(name) { }

        #region IBuiltinPythonModule Members

        public IMember GetAnyMember(string name) {
            return null;
        }

        #endregion

        #region IPythonModule Members

        public IEnumerable<string> GetChildrenModules() {
            yield break;
        }

        public void Imported(IModuleContext context) {
        }
        #endregion
    }
}
