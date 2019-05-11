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

using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Values {
    internal sealed class GlobalScope: Scope, IGlobalScope {
        public GlobalScope(IPythonModule module): base(null, null, module) {
            DeclareBuiltinVariables();
        }

        public override ScopeStatement Node => Module.Analysis?.Ast;

        private void DeclareBuiltinVariables() {
            if (Module.ModuleType != ModuleType.User) {
                return;
            }

            var boolType = Module.Interpreter.GetBuiltinType(BuiltinTypeId.Bool);
            var strType = Module.Interpreter.GetBuiltinType(BuiltinTypeId.Str);
            var listType = Module.Interpreter.GetBuiltinType(BuiltinTypeId.List);
            var dictType = Module.Interpreter.GetBuiltinType(BuiltinTypeId.Dict);

            DeclareVariable("__debug__", boolType, VariableSource.Builtin);
            DeclareVariable("__doc__", strType, VariableSource.Builtin);
            DeclareVariable("__file__", strType, VariableSource.Builtin);
            DeclareVariable("__name__", strType, VariableSource.Builtin);
            DeclareVariable("__package__", strType, VariableSource.Builtin);
            DeclareVariable("__path__", listType, VariableSource.Builtin);
            DeclareVariable("__dict__", dictType, VariableSource.Builtin);
        }
    }
}
