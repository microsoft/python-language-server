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
using Microsoft.Python.Analysis.Specializations.Typing.Types;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Specializations.Typing.Values {
    /// <summary>
    /// Specialization of TypeVar().
    /// </summary>
    internal sealed class TypeVar : SpecializedClass {
        private readonly IPythonFunctionType _constructor;

        public TypeVar(IPythonModule declaringModule) : base(BuiltinTypeId.Type, declaringModule) {
            var interpreter = DeclaringModule.Interpreter;

            var fn = new PythonFunctionType("__init__", new Location(declaringModule), this, "TypeVar");
            var o = new PythonFunctionOverload(fn, new Location(DeclaringModule));

            var boolType = interpreter.GetBuiltinType(BuiltinTypeId.Bool);
            o.SetParameters(new List<ParameterInfo> {
                new ParameterInfo("self", this, ParameterKind.Normal, this),
                new ParameterInfo("name", interpreter.GetBuiltinType(BuiltinTypeId.Str), ParameterKind.Normal, null),
                new ParameterInfo("constraints", interpreter.GetBuiltinType(BuiltinTypeId.Str), ParameterKind.List, null),
                new ParameterInfo("bound", interpreter.GetBuiltinType(BuiltinTypeId.Type), ParameterKind.KeywordOnly, new PythonConstant(null, interpreter.GetBuiltinType(BuiltinTypeId.None))),
                new ParameterInfo("covariant", boolType, ParameterKind.KeywordOnly, new PythonConstant(false, boolType)),
                new ParameterInfo("contravariant", boolType, ParameterKind.KeywordOnly, new PythonConstant(false, boolType))
            });
            fn.AddOverload(o);
            _constructor = fn;
        }

        public override IMember GetMember(string name)
            => name == _constructor.Name ? _constructor : base.GetMember(name);

        public override IMember CreateInstance(IArgumentSet args) 
            => GenericTypeParameter.FromTypeVar(args, args.Eval?.Module ?? DeclaringModule);
    }
}
