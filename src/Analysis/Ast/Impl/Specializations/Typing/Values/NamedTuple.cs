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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Specializations.Typing.Values {
    /// <summary>
    /// Specialization of NamedTuple().
    /// </summary>
    internal sealed class NamedTuple : SpecializedClass {
        private readonly IPythonFunctionType _constructor;

        public NamedTuple(IPythonModule declaringModule) : base(BuiltinTypeId.Tuple, declaringModule) {
            var interpreter = DeclaringModule.Interpreter;

            var fn = new PythonFunctionType("__init__", new Location(declaringModule), this, "NamedTuple");
            var o = new PythonFunctionOverload(fn, new Location(DeclaringModule));

            o.SetParameters(new List<ParameterInfo> {
                new ParameterInfo("self", this, ParameterKind.Normal, this),
                new ParameterInfo("name", interpreter.GetBuiltinType(BuiltinTypeId.Str), ParameterKind.Normal, null),
                new ParameterInfo("members", interpreter.GetBuiltinType(BuiltinTypeId.List), ParameterKind.Normal, null)
            });
            fn.AddOverload(o);
            _constructor = fn;
        }

        public override IMember GetMember(string name) 
            => name == _constructor.Name ? _constructor : base.GetMember(name);

        public override IMember CreateInstance(IArgumentSet args)
            => CreateNamedTuple(args.Values<IMember>(), DeclaringModule, default);

        private IPythonType CreateNamedTuple(IReadOnlyList<IMember> typeArgs, IPythonModule declaringModule, IndexSpan indexSpan) {
            // For class argument list includes 'self'
            if (typeArgs.Count != 3) {
                // TODO: report wrong number of arguments
                return DeclaringModule.Interpreter.UnknownType;
            }

            ;
            if (!typeArgs[1].TryGetConstant<string>(out var tupleName) || string.IsNullOrEmpty(tupleName)) {
                // TODO: report name is incorrect.
                return DeclaringModule.Interpreter.UnknownType;
            }

            var argList = (typeArgs[2] as IPythonCollection)?.Contents;
            if (argList == null) {
                // TODO: report type spec is not a list.
                return DeclaringModule.Interpreter.UnknownType;
            }

            var itemNames = new List<string>();
            var itemTypes = new List<IPythonType>();
            foreach (var a in argList) {
                if (a.TryGetConstant(out string itemName1)) {
                    // Not annotated
                    itemNames.Add(itemName1);
                    itemTypes.Add(DeclaringModule.Interpreter.UnknownType);
                    continue;
                }

                // Now assume annotated pair that comes as a tuple.
                if (!(a is IPythonCollection c) || c.Type.TypeId != BuiltinTypeId.Tuple) {
                    // TODO: report that item is not a tuple.
                    continue;
                }
                if (c.Contents.Count != 2) {
                    // TODO: report extra items in the element spec.
                    continue;
                }
                if (!c.Contents[0].TryGetConstant<string>(out var itemName2)) {
                    // TODO: report item name is not a string.
                    continue;
                }

                itemNames.Add(itemName2);
                itemTypes.Add(c.Contents[1].GetPythonType());
            }
            return TypingTypeFactory.CreateNamedTupleType(tupleName, itemNames, itemTypes, declaringModule, indexSpan);
        }
    }
}
