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
using System.Threading;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Generators {
    public sealed partial class StubGenerator {
        private sealed class TypeInfoWalker : BaseWalker {
            private readonly Stack<IPythonType> _stack;

            public TypeInfoWalker(ILogger logger, IPythonModule module, PythonAst ast, string original, CancellationToken cancellationToken)
                : base(logger, module, ast, original, cancellationToken) {
                _stack = new Stack<IPythonType>();
            }

            public override bool Walk(ClassDefinition node, Node parent) {
                // no type variables for now
                var member = GetMember(node);
                _stack.Push(member);

                return member != null;
            }

            public override void PostWalk(ClassDefinition node, Node parent) {
                _stack.Pop();
            }

            public override bool Walk(FunctionDefinition node, Node parent) {
                // for now, just do poorman's type info
                var member = GetMember(node) as IPythonFunctionType;
                if (member == null) {
                    // no matching member
                    return false;
                }

                if (member.Overloads.Count == 1) {
                    // if there is only 1, we just assume it is same fuction
                    AppendMethod(node, member.Overloads[0]);
                    return false;
                }

                var overload = member.Overloads.FirstOrDefault(m => m.Parameters.Count == node.Parameters.Length);
                if (overload != null) {
                    AppendMethod(node, overload);
                }

                return false;
            }

            private IPythonType GetMember(ScopeStatement node) {
                var parent = _stack.Count > 0 ? _stack.Peek() : null;
                if (parent != null) {
                    return parent.GetMember(node.Name) as IPythonType;
                }

                return Module.GetMember(node.Name) as IPythonType;
            }

            private void AppendMethod(FunctionDefinition node, IPythonFunctionOverload method) {
                var member = method.ClassMember as IPythonFunctionType;
                var skip = member.IsStatic ? 0 : 1;

                foreach (var parameter in node.Parameters.Skip(skip)) {
                    var info = method.Parameters.FirstOrDefault(p => p.Name == parameter.Name);

                    if (parameter.Annotation == null && info?.Type != null) {
                        // for now, don't resolve type name. we could do poorman's 
                        // create minimally qualified type name or a way to get string representation
                        // of type at current context using imports. but for now, nothing
                        // since I don't have ast for library, I can't use original code
                        var nameSpan = parameter.NameExpression?.IndexSpan ?? parameter.IndexSpan;
                        AppendOriginalText(nameSpan.End);
                        AppendText($" : {info.Type.Name}", nameSpan.End);
                    }

                    if (parameter.DefaultValue == null && info?.DefaultValueString != null) {
                        // for now, don't resolve type name. we could do poorman's 
                        // create minimally qualified type name or a way to get string representation
                        // of type at current context using imports. but for now, nothing
                        // since I don't have ast for library, I can't use original code
                        AppendOriginalText(parameter.IndexSpan.End);

                        AppendText($" = {info.DefaultValueString}", parameter.IndexSpan.End);
                    }
                }

                var constant = method.StaticReturnValue as IPythonConstant;
                if (node.ReturnAnnotation == null && constant != null) {
                    // for now, don't resolve type name. we could do poorman's 
                    // create minimally qualified type name or a way to get string representation
                    // of type at current context using imports. but for now, nothing
                    // since I don't have ast for library, I can't use original code
                    AppendOriginalText(node.HeaderIndex);
                    AppendText($" -> {constant.Type.Name}", node.HeaderIndex);
                }
            }
        }
    }
}
