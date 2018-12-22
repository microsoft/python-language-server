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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    [DebuggerDisplay("{Target.Name}")]
    class AnalysisFunctionWalker : PythonWalkerAsync {
        private readonly ExpressionLookup _lookup;
        private readonly Scope _parentScope;
        private readonly PythonFunctionOverload _overload;
        private IPythonClass _self;

        public AnalysisFunctionWalker(
            ExpressionLookup lookup,
            FunctionDefinition targetFunction,
            PythonFunctionOverload overload
        ) {
            _lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
            Target = targetFunction ?? throw new ArgumentNullException(nameof(targetFunction));
            _overload = overload ?? throw new ArgumentNullException(nameof(overload));
            _parentScope = _lookup.CurrentScope;
        }

        public FunctionDefinition Target { get; }

        public async Task WalkAsync(CancellationToken cancellationToken = default) {
            using (_lookup.OpenScope(_parentScope)) {
                _self = await GetSelfAsync(cancellationToken);
                using (_lookup.CreateScope(Target, _parentScope)) {

                    var annotationType = _lookup.GetTypeFromAnnotation(Target.ReturnAnnotation);
                    if (annotationType != null) {
                        _overload.AddReturnType(annotationType);
                    }

                    // Declare self, if any
                    var skip = 0;
                    if (_self != null) {
                        var p0 = Target.Parameters.FirstOrDefault();
                        if (p0 != null && !string.IsNullOrEmpty(p0.Name)) {
                            _lookup.DeclareVariable(p0.Name, _self, p0.NameExpression);
                            skip++;
                        }
                    }

                    // Declare parameters in scope
                    for(var i = skip; i < Target.Parameters.Length; i++) {
                        var p = Target.Parameters[i];
                        if (!string.IsNullOrEmpty(p.Name)) {
                            var defaultValue = await _lookup.GetValueFromExpressionAsync(p.DefaultValue, cancellationToken) ?? _lookup.UnknownType;
                            var argType = new CallableArgumentType(i, defaultValue.GetPythonType());
                            _lookup.DeclareVariable(p.Name, argType, p.NameExpression);
                        }
                    }

                    // return type from the annotation always wins, no need to walk the body.
                    if (annotationType == null) {
                        await Target.WalkAsync(this, cancellationToken);
                    }
                } // Function scope
            } // Restore original scope at the entry
        }

        public override Task<bool> WalkAsync(FunctionDefinition node, CancellationToken cancellationToken = default) {
            if (node != Target) {
                // Do not walk nested functions (yet)
                return Task.FromResult(false);
            }

            if (_overload.Documentation == null) {
                var docNode = (node.Body as SuiteStatement)?.Statements.FirstOrDefault();
                var ce = (docNode as ExpressionStatement)?.Expression as ConstantExpression;
                if (ce?.Value is string doc) {
                    _overload.SetDocumentation(doc);
                }
            }

            return Task.FromResult(true);
        }

        public override async Task<bool> WalkAsync(AssignmentStatement node, CancellationToken cancellationToken = default) {
            var value = await _lookup.GetValueFromExpressionAsync(node.Right, cancellationToken);

            if (node.Left.FirstOrDefault() is TupleExpression tex) {
                // Tuple = Tuple. Transfer values.
                var texHandler = new TupleExpressionHandler(_lookup);
                await texHandler.HandleTupleAssignmentAsync(tex, node.Right, value.GetPythonType(), cancellationToken);
                return await base.WalkAsync(node, cancellationToken);
            }

            foreach (var lhs in node.Left) {
                if (lhs is MemberExpression memberExp && memberExp.Target is NameExpression nameExp1) {
                    if (_self is PythonType t && nameExp1.Name == "self") {
                        t.AddMembers(new[] { new KeyValuePair<string, IPythonType>(memberExp.Name, value.GetPythonType()) }, true);
                    }
                    continue;
                }

                if (lhs is NameExpression nameExp2 && nameExp2.Name == "self") {
                    continue; // Don't assign to 'self'
                }

                // Basic assignment
                foreach (var ne in node.Left.OfType<NameExpression>()) {
                    _lookup.DeclareVariable(ne.Name, value, ne);
                }
            }
            return await base.WalkAsync(node, cancellationToken);
        }

        public override Task<bool> WalkAsync(IfStatement node, CancellationToken cancellationToken = default) {
            // Handle basic check such as
            // if isinstance(value, type):
            //    return value
            // by assigning type to the value unless clause is raising exception.
            var ce = node.Tests.FirstOrDefault()?.Test as CallExpression;
            if (ce?.Target is NameExpression ne && ne?.Name == "isinstance" && ce.Args.Count == 2) {
                var nex = ce.Args[0].Expression as NameExpression;
                var name = nex?.Name;
                var typeName = (ce.Args[1].Expression as NameExpression)?.Name;
                if (name != null && typeName != null) {
                    var typeId = typeName.GetTypeId();
                    if (typeId != BuiltinTypeId.Unknown) {
                        _lookup.DeclareVariable(name, new PythonType(typeName, typeId), nex);
                    }
                }
            }
            return base.WalkAsync(node, cancellationToken);
        }

        public override async Task<bool> WalkAsync(ReturnStatement node, CancellationToken cancellationToken = default) {
            var value = await _lookup.GetValueFromExpressionAsync(node.Expression, cancellationToken);
            var t = _lookup.GetTypeFromValue(value.GetPythonType());
            if (t != null) {
                _overload.AddReturnType(t);
            }
            return true; // We want to evaluate all code so all private variables in __new__ get defined
        }

        private struct MethodInfo {
            public bool isClassMethod;
            public bool isStaticMethod;
        }

        private async Task<IPythonClass> GetSelfAsync(CancellationToken cancellationToken = default) {
            var info = await GetMethodInfoAsync(cancellationToken);
            var self = _lookup.LookupNameInScopes("__class__", ExpressionLookup.LookupOptions.Local);
            return !info.isStaticMethod && !info.isClassMethod
                 ? self as IPythonClass
                 : null;
        }

        private async Task<MethodInfo> GetMethodInfoAsync(CancellationToken cancellationToken = default) {
            var info = new MethodInfo();

            if (Target.IsLambda) {
                info.isStaticMethod = true;
                return info;
            }

            var classMethodObj = _lookup.Interpreter.GetBuiltinType(BuiltinTypeId.ClassMethod);
            var staticMethodObj = _lookup.Interpreter.GetBuiltinType(BuiltinTypeId.StaticMethod);
            foreach (var d in (Target.Decorators?.Decorators).MaybeEnumerate().ExcludeDefault()) {
                var m = await _lookup.GetValueFromExpressionAsync(d, cancellationToken);
                if (classMethodObj.Equals(m)) {
                    info.isClassMethod = true;
                } else if (staticMethodObj.Equals(m)) {
                    info.isStaticMethod = true;
                }
            }
            return info;
        }
    }
}
