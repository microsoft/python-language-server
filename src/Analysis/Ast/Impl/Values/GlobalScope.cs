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
        private readonly PythonAst _ast;

        public GlobalScope(IPythonModule module, PythonAst ast): base(null, null, module) {
            _ast = ast;
            DeclareBuiltinVariables();
        }

        public override ScopeStatement Node => _ast;

        private void DeclareBuiltinVariables() {
            if (Module.ModuleType != ModuleType.User) {
                return;
            }

            var location = new Location(Module);

            var boolType = Module.Interpreter.GetBuiltinType(BuiltinTypeId.Bool);
            var strType = Module.Interpreter.GetBuiltinType(BuiltinTypeId.Str);
            var listType = Module.Interpreter.GetBuiltinType(BuiltinTypeId.List);
            var dictType = Module.Interpreter.GetBuiltinType(BuiltinTypeId.Dict);
            var objectType = Module.Interpreter.GetBuiltinType(BuiltinTypeId.Object);

            DeclareVariable("__debug__", boolType, VariableSource.Builtin, location);
            DeclareVariable("__doc__", strType, VariableSource.Builtin, location);
            DeclareVariable("__file__", strType, VariableSource.Builtin, location);
            DeclareVariable("__name__", strType, VariableSource.Builtin, location);
            DeclareVariable("__package__", strType, VariableSource.Builtin, location);
            DeclareVariable("__path__", listType, VariableSource.Builtin, location);
            DeclareVariable("__dict__", dictType, VariableSource.Builtin, location);
            DeclareVariable("__spec__", objectType, VariableSource.Builtin, location);
        }
    }
}
