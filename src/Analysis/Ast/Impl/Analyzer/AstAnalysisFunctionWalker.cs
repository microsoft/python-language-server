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
using Microsoft.Python.Analysis.Analyzer.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    [DebuggerDisplay("{Target.Name}")]
    class AstAnalysisFunctionWalker : PythonWalker {
        private readonly ExpressionLookup _lookup;
        private readonly Scope _parentScope;
        private readonly AstPythonFunctionOverload _overload;
        private AstPythonType _selfType;

        public AstAnalysisFunctionWalker(
            ExpressionLookup lookup,
            FunctionDefinition targetFunction,
            AstPythonFunctionOverload overload
        ) {
            _lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
            Target = targetFunction ?? throw new ArgumentNullException(nameof(targetFunction));
            _overload = overload ?? throw new ArgumentNullException(nameof(overload));
            _parentScope = _lookup.CurrentScope;
        }

        public FunctionDefinition Target { get; } 

        private void GetMethodType(out bool classMethod, out bool staticMethod) {
            classMethod = false;
            staticMethod = false;

            if (Target.IsLambda) {
                staticMethod = true;
                return;
            }

            var classMethodObj = _lookup.Interpreter.GetBuiltinType(BuiltinTypeId.ClassMethod);
            var staticMethodObj = _lookup.Interpreter.GetBuiltinType(BuiltinTypeId.StaticMethod);
            foreach (var d in (Target.Decorators?.Decorators).MaybeEnumerate().ExcludeDefault()) {
                var m = _lookup.GetValueFromExpression(d);
                if (m.Equals(classMethodObj)) {
                    classMethod = true;
                } else if (m.Equals(staticMethodObj)) {
                    staticMethod = true;
                }
            }
        }

        public void Walk() {
            var self = GetSelf();
            _selfType = (self as AstPythonConstant)?.Type as AstPythonType;

            var annotationTypes = _lookup.GetTypesFromAnnotation(Target.ReturnAnnotation).ExcludeDefault().ToArray();
            _overload.ReturnTypes.AddRange(annotationTypes);

            _lookup.OpenScope(Target, _parentScope);

            // Declare self, if any
            var skip = 0;
            if (self != null) {
                var p0 = Target.Parameters.FirstOrDefault();
                if (p0 != null && !string.IsNullOrEmpty(p0.Name)) {
                    _lookup.DeclareVariable(p0.Name, self);
                    skip++;
                }
            }

            // Declare parameters in scope
            foreach(var p in Target.Parameters.Skip(skip).Where(p => !string.IsNullOrEmpty(p.Name))) {
                 var value = _lookup.GetValueFromExpression(p.DefaultValue);
                _lookup.DeclareVariable(p.Name, value ?? _lookup.UnknownType);
            }

            // return type from the annotation always wins, no need to walk the body.
            if (!annotationTypes.Any()) {
                Target.Walk(this);
            }
            _lookup.CloseScope();
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
            var value = _lookup.GetValueFromExpression(node.Right);
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
                    _lookup.DeclareVariable(ne.Name, value);
                }

                // Tuple = Tuple. Transfer values.
                if (lhs is TupleExpression tex) {
                    if (value is TupleExpression valTex) {
                        var returnedExpressions = valTex.Items.ToArray();
                        var names = tex.Items.Select(x => (x as NameExpression)?.Name).ToArray();
                        for (var i = 0; i < Math.Min(names.Length, returnedExpressions.Length); i++) {
                            if (returnedExpressions[i] != null) {
                                var v = _lookup.GetValueFromExpression(returnedExpressions[i]);
                                _lookup.DeclareVariable(names[i], v);
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
                                _lookup.DeclareVariable(names[i], new AstPythonConstant(types[i]));
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
                        _lookup.DeclareVariable(name, 
                            new AstPythonConstant(new AstPythonType(typeName, typeId)));
                    }
                }
            }
            return base.Walk(node);
        }

        public override bool Walk(ReturnStatement node) {
            var types = _lookup.GetTypesFromValue(_lookup.GetValueFromExpression(node.Expression)).ExcludeDefault();
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
            GetMethodType(out var classMethod, out var staticMethod);
            var self = _lookup.LookupNameInScopes("__class__", ExpressionLookup.LookupOptions.Local);
            if (!staticMethod && !classMethod) {
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
