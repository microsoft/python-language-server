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

using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonBuiltinType : AstPythonType {
        private BuiltinTypeId _typeId;

        public AstPythonBuiltinType(string name, BuiltinTypeId typeId)
            : base(name) {
            _typeId = typeId;
        }

        public AstPythonBuiltinType(
            string name,
            PythonAst ast,
            IPythonModule declModule,
            int startIndex,
            string doc,
            LocationInfo loc,
            BuiltinTypeId typeId = BuiltinTypeId.Unknown,
            bool isClass = false
        ) : base(name, ast, declModule, startIndex, doc, loc, isClass) {
            _typeId = typeId;
        }

        public bool TrySetTypeId(BuiltinTypeId typeId) {
            if (_typeId != BuiltinTypeId.Unknown) {
                return false;
            }
            _typeId = typeId;
            return true;
        }

        public override bool IsBuiltin => true;
        public override BuiltinTypeId TypeId => _typeId;

        public bool IsHidden {
            get {
                lock (Members) {
                    return Members.ContainsKey("__hidden__");
                }
            }
        }
    }
}
