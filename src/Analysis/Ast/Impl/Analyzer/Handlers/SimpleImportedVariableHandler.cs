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
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Analyzer.Handlers {
    internal sealed class SimpleImportedVariableHandler : IImportedVariableHandler {
        public static IImportedVariableHandler Instance { get; } = new SimpleImportedVariableHandler();

        private SimpleImportedVariableHandler() {}

        public IEnumerable<string> GetMemberNames(PythonVariableModule variableModule)
            => variableModule.Analysis.StarImportMemberNames ?? variableModule.GetMemberNames().Where(s => !s.StartsWithOrdinal("_"));

        public IMember GetVariable(in PythonVariableModule module, in string memberName) {
            // First try exported or child submodules.
            var value = module.GetMember(memberName);

            // Value may be variable or submodule. If it is variable, we need it in order to add reference.
            var variable = module.Analysis?.GlobalScope?.Variables[memberName];
            value = variable?.Value?.Equals(value) == true ? variable : value;

            return value ?? module.Interpreter.UnknownType;
        }

        public void EnsureModule(in PythonVariableModule module) { }
    }
}
