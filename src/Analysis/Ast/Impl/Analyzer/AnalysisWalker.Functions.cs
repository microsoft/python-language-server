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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    internal sealed partial class AnalysisWalker {
        public override async Task<bool> WalkAsync(FunctionDefinition node, CancellationToken cancellationToken = default) {
            if (node.IsLambda || _replacedByStubs.Contains(node)) {
                return false;
            }

            var dec = (node.Decorators?.Decorators).MaybeEnumerate().ExcludeDefault().ToArray();
            foreach (var d in dec) {
                var member = await _lookup.GetValueFromExpressionAsync(d, cancellationToken);
                var memberType = member?.GetPythonType();
                if (memberType != null) {
                    var declaringModule = memberType.DeclaringModule;

                    if (memberType.TypeId == BuiltinTypeId.Property) {
                        AddProperty(node, declaringModule, memberType);
                        return false;
                    }

                    var name = memberType.Name;
                    if (declaringModule?.Name == "abc" && name == "abstractproperty") {
                        AddProperty(node, declaringModule, memberType);
                        return false;
                    }
                }
            }

            foreach (var setter in dec.OfType<MemberExpression>().Where(n => n.Name == "setter")) {
                if (setter.Target is NameExpression src) {
                    if (_lookup.LookupNameInScopes(src.Name, ExpressionLookup.LookupOptions.Local) is PythonProperty existingProp) {
                        // Setter for an existing property, so don't create a function
                        existingProp.MakeSettable();
                        return false;
                    }
                }
            }

            ProcessFunctionDefinition(node);
            // Do not recurse into functions
            return false;
        }

        public void ProcessFunctionDefinition(FunctionDefinition node) {
            if (!(_lookup.LookupNameInScopes(node.Name, ExpressionLookup.LookupOptions.Local) is PythonFunction existing)) {
                var cls = _lookup.GetInScope("__class__");
                var loc = GetLoc(node);
                existing = new PythonFunction(node, _module, cls.GetPythonType(), loc);
                _lookup.DeclareVariable(node.Name, existing, loc);
            }
            AddOverload(node, o => existing.AddOverload(o));
        }

        private void AddProperty(FunctionDefinition node, IPythonModule declaringModule, IPythonType declaringType) {
            if (!(_lookup.LookupNameInScopes(node.Name, ExpressionLookup.LookupOptions.Local) is PythonProperty existing)) {
                var loc = GetLoc(node);
                existing = new PythonProperty(node, declaringModule, declaringType, loc);
                _lookup.DeclareVariable(node.Name, existing, loc);
            }

            AddOverload(node, o => existing.AddOverload(o));
        }

        private PythonFunctionOverload CreateFunctionOverload(ExpressionLookup lookup, FunctionDefinition node) {
            var parameters = node.Parameters
                .Select(p => new ParameterInfo(_ast, p, _lookup.GetTypeFromAnnotation(p.Annotation)))
                .ToArray();

            var overload = new PythonFunctionOverload(
                node.Name,
                parameters,
                lookup.GetLocOfName(node, node.NameExpression),
                node.ReturnAnnotation?.ToCodeString(_ast));

            _functionWalkers.Add(new AnalysisFunctionWalker(_module, lookup, node, overload));
            return overload;
        }

        private static string GetDoc(SuiteStatement node) {
            var docExpr = node?.Statements?.FirstOrDefault() as ExpressionStatement;
            var ce = docExpr?.Expression as ConstantExpression;
            return ce?.Value as string;
        }

        private void AddOverload(FunctionDefinition node, Action<IPythonFunctionOverload> addOverload) {
            // Check if function exists in stubs. If so, take overload from stub
            // and the documentation from this actual module.
            var stubOverload = GetOverloadFromStub(node);
            if (stubOverload != null) {
                if (!string.IsNullOrEmpty(node.Documentation)) {
                    stubOverload.SetDocumentation(node.Documentation);
                }
                addOverload(stubOverload);
                _replacedByStubs.Add(node);
                return;
            }

            if (!_functionWalkers.Contains(node)) {
                var overload = CreateFunctionOverload(_lookup, node);
                if (overload != null) {
                    addOverload(overload);
                }
            }
        }

        private PythonFunctionOverload GetOverloadFromStub(FunctionDefinition node) {
            if (_module.Stub == null) {
                return null;
            }

            var memberNameChain = new List<string>(Enumerable.Repeat(node.Name, 1));
            IScope scope = _lookup.CurrentScope;

            while (scope != _globalScope) {
                memberNameChain.Add(scope.Name);
                scope = scope.OuterScope;
            }

            IPythonType t = _module.Stub;
            for (var i = memberNameChain.Count - 1; i >= 0; i--) {
                t = t.GetMember(memberNameChain[i]).GetPythonType();
                if (t == null) {
                    return null;
                }
            }

            if (t is IPythonFunction f) {
                return f.Overloads
                    .OfType<PythonFunctionOverload>()
                    .FirstOrDefault(o => o.Parameters.Count == node.Parameters.Length);
            }

            return null;
        }
    }
}
