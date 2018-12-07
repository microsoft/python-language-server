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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    [DebuggerDisplay("{Target.Name}")]
    class AstAnalysisFunctionWalker : PythonWalker {
        private readonly NameLookupContext _scope;
        private readonly AstPythonFunctionOverload _overload;
        private AstPythonType _selfType;

        public AstAnalysisFunctionWalker(
            NameLookupContext scope,
            FunctionDefinition targetFunction,
            AstPythonFunctionOverload overload
        ) {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
            Target = targetFunction ?? throw new ArgumentNullException(nameof(targetFunction));
            _overload = overload ?? throw new ArgumentNullException(nameof(overload));
        }

        public FunctionDefinition Target { get; } 

        private void GetMethodType(out bool classmethod, out bool staticmethod) {
            classmethod = false;
            staticmethod = false;

            if (Target.IsLambda) {
                staticmethod = true;
                return;
            }

            var classmethodObj = _scope.Interpreter.GetBuiltinType(BuiltinTypeId.ClassMethod);
            var staticmethodObj = _scope.Interpreter.GetBuiltinType(BuiltinTypeId.StaticMethod);
            foreach (var d in (Target.Decorators?.Decorators).MaybeEnumerate().ExcludeDefault()) {
                var m = _scope.GetValueFromExpression(d);
                if (m == classmethodObj) {
                    classmethod = true;
                } else if (m == staticmethodObj) {
                    staticmethod = true;
                }
            }
        }

        public void Walk() {
            var self = GetSelf();
            _selfType = (self as AstPythonConstant)?.Type as AstPythonType;

            var annotationTypes = _scope.GetTypesFromAnnotation(Target.ReturnAnnotation).ExcludeDefault();
            _overload.ReturnTypes.AddRange(annotationTypes);

            _scope.PushScope();

            // Declare self, if any
            var skip = 0;
            if (self != null) {
                var p0 = Target.Parameters.FirstOrDefault();
                if (p0 != null && !string.IsNullOrEmpty(p0.Name)) {
                    _scope.SetInScope(p0.Name, self);
                    skip++;
                }
            }

            // Declare parameters in scope
            foreach(var p in Target.Parameters.Skip(skip).Where(p => !string.IsNullOrEmpty(p.Name))) {
                 var value = _scope.GetValueFromExpression(p.DefaultValue);
                _scope.SetInScope(p.Name, value ?? _scope.UnknownType);
            }

            // return type from the annotation always wins, no need to walk the body.
            if (!annotationTypes.Any()) {
                Target.Walk(this);
            }
            _scope.PopScope();
        }

        public override bool Walk(FunctionDefinition node) {
            if (node != Target) {
                // Do not walk nested functions (yet)
                return false;
            }

            if (_overload.Documentation == null) {
                var docNode = (node.Body as SuiteStatement)?.Statements.FirstOrDefault();
                var ce = (docNode as ExpressionStatement)?.Expression as ConstantExpression;
                if (ce?.Value is string doc) {
                    _overload.SetDocumentation(doc);
                }
            }

            return true;
        }

        public override bool Walk(AssignmentStatement node) {
            var value = _scope.GetValueFromExpression(node.Right);
            foreach (var lhs in node.Left) {
                if (lhs is MemberExpression memberExp && memberExp.Target is NameExpression nameExp1) {
                    if (_selfType != null && nameExp1.Name == "self") {
                        _selfType.AddMembers(new[] { new KeyValuePair<string, IMember>(memberExp.Name, value) }, true);
                    }
                    continue;
                }

                if (lhs is NameExpression nameExp2 && nameExp2.Name == "self") {
                    continue; // Don't assign to 'self'
                }

                // Basic assignment
                foreach (var ne in node.Left.OfType<NameExpression>()) {
                    _scope.SetInScope(ne.Name, value);
                }

                // Tuple = Tuple. Transfer values.
                if (lhs is TupleExpression tex) {
                    if (value is TupleExpression valTex) {
                        var returnedExpressions = valTex.Items.ToArray();
                        var names = tex.Items.Select(x => (x as NameExpression)?.Name).ToArray();
                        for (var i = 0; i < Math.Min(names.Length, returnedExpressions.Length); i++) {
                            if (returnedExpressions[i] != null) {
                                var v = _scope.GetValueFromExpression(returnedExpressions[i]);
                                _scope.SetInScope(names[i], v);
                            }
                        }
                        continue;
                    }

                    // Tuple = 'tuple value' (such as from callable). Transfer values.
                    if (value is AstPythonConstant c && c.Type is AstPythonSequence seq) {
                        var types = seq.IndexTypes.ToArray();
                        var names = tex.Items.Select(x => (x as NameExpression)?.Name).ToArray();
                        for (var i = 0; i < Math.Min(names.Length, types.Length); i++) {
                            if (names[i] != null && types[i] != null) {
                                _scope.SetInScope(names[i], new AstPythonConstant(types[i]));
                            }
                        }
                    }
                }
            }
            return base.Walk(node);
        }

        public override bool Walk(IfStatement node) {
            // Handle basic check such as
            // if isinstance(value, type):
            //    return value
            // by assigning type to the value unless clause is raising exception.
            var ce = node.Tests.FirstOrDefault()?.Test as CallExpression;
            if (ce?.Target is NameExpression ne && ne?.Name == "isinstance" && ce.Args.Count == 2) {
                var name = (ce.Args[0].Expression as NameExpression)?.Name;
                var typeName = (ce.Args[1].Expression as NameExpression)?.Name;
                if (name != null && typeName != null) {
                    var typeId = typeName.GetTypeId();
                    if (typeId != BuiltinTypeId.Unknown) {
                        _scope.SetInScope(name, 
                            new AstPythonConstant(new AstPythonType(typeName, typeId)));
                    }
                }
            }
            return base.Walk(node);
        }

        public override bool Walk(ReturnStatement node) {
            var types = _scope.GetTypesFromValue(_scope.GetValueFromExpression(node.Expression)).ExcludeDefault();
            foreach (var type in types) {
                _overload.ReturnTypes.Add(type);
            }
            
            // Clean up: if there are None or Unknown types along with real ones, remove them.
            var realTypes = _overload.ReturnTypes
                .Where(t => t.TypeId != BuiltinTypeId.Unknown && t.TypeId != BuiltinTypeId.NoneType)
                .ToList();

            if (realTypes.Count > 0) {
                _overload.ReturnTypes.Clear();
                _overload.ReturnTypes.AddRange(realTypes);
            }
            return true; // We want to evaluate all code so all private variables in __new__ get defined
        }

        private IMember GetSelf() {
            GetMethodType(out var classmethod, out var staticmethod);
            var self = _scope.LookupNameInScopes("__class__", NameLookupContext.LookupOptions.Local);
            if (!staticmethod && !classmethod) {
                if (!(self is IPythonType cls)) {
                    self = null;
                } else {
                    self = new AstPythonConstant(cls, ((cls as ILocatedMember)?.Locations).MaybeEnumerate().ToArray());
                }
            }
            return self;
        }
    }
}
