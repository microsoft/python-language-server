﻿// Copyright(c) Microsoft Corporation
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
using Microsoft.Python.Analysis.Analyzer.Evaluation;
using Microsoft.Python.Analysis.Extensions;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Analysis.Values.Collections;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Symbols {
    [DebuggerDisplay("{FunctionDefinition.Name}")]
    internal sealed class FunctionEvaluator : MemberEvaluator {
        private readonly IPythonClassMember _function;
        private readonly PythonFunctionOverload _overload;
        private readonly IPythonClassType _self;
        private bool _isConstructor;

        public FunctionEvaluator(ExpressionEval eval, PythonFunctionOverload overload) 
            : base(eval, overload.FunctionDefinition) {

            _overload = overload;
            _function = overload.ClassMember ?? throw new NullReferenceException(nameof(overload.ClassMember));
            _self = _function.DeclaringType as PythonClassType;

            FunctionDefinition = overload.FunctionDefinition;
        }

        public FunctionDefinition FunctionDefinition { get; }

        public override void Evaluate() {
            var stub = SymbolTable.ReplacedByStubs.Contains(Target)
                       || _function.DeclaringModule.ModuleType == ModuleType.Stub
                       || Module.ModuleType == ModuleType.Specialized;

            using (Eval.OpenScope(_function.DeclaringModule, FunctionDefinition, out _)) {
                // Process annotations.
                var annotationType = Eval.GetTypeFromAnnotation(FunctionDefinition.ReturnAnnotation);
                if (!annotationType.IsUnknown()) {
                    // Annotations are typically types while actually functions return
                    // instances unless specifically annotated to a type such as Type[T].
                    var instance = annotationType.CreateInstance(annotationType.Name, ArgumentSet.Empty);
                    _overload.SetReturnValue(instance, true);
                } else {
                    // Check if function is a generator
                    var suite = FunctionDefinition.Body as SuiteStatement;
                    var yieldExpr = suite?.Statements.OfType<ExpressionStatement>().Select(s => s.Expression as YieldExpression).ExcludeDefault().FirstOrDefault();
                    if (yieldExpr != null) {
                        // Function return is an iterator
                        var yieldValue = Eval.GetValueFromExpression(yieldExpr.Expression) ?? Eval.UnknownType;
                        var returnValue = new PythonGenerator(Eval.Interpreter, yieldValue);
                        _overload.SetReturnValue(returnValue, true);
                    }
                }

                if (!stub) {
                    // Do process body of constructors since they may be declaring
                    // variables that are later used to determine return type of other
                    // methods and properties.
                    _isConstructor = _function.Name.EqualsOrdinal("__init__") || _function.Name.EqualsOrdinal("__new__");
                    if (_isConstructor || annotationType.IsUnknown() || Module.ModuleType == ModuleType.User) {
                        // Return type from the annotation is sufficient for libraries
                        // and stubs, no need to walk the body.
                        DeclareParameters(true);
                        FunctionDefinition.Body?.Walk(this);
                    }
                }
            }
            Result = _function;
        }

        public override bool Walk(AssignmentStatement node) {
            // Skip over bodies in libraries except when it is a constructor.
            if (!_isConstructor && _function.DeclaringModule.ModuleType == ModuleType.Library) {
                return false;
            }

            var value = Eval.GetValueFromExpression(node.Right) ?? Eval.UnknownType;
            foreach (var lhs in node.Left) {
                switch (lhs) {
                    case MemberExpression memberExp when memberExp.Target is NameExpression nameExp1: {
                            if (_function.DeclaringType.GetPythonType() is PythonClassType t && nameExp1.Name == "self") {
                                t.AddMembers(new[] { new KeyValuePair<string, IMember>(memberExp.Name, value) }, false);
                            }
                            continue;
                        }
                    case NameExpression nameExp2 when nameExp2.Name == "self":
                        return true; // Don't assign to 'self'
                }
            }
            return base.Walk(node);
        }

        public override bool Walk(ReturnStatement node) {
            var value = Eval.GetValueFromExpression(node.Expression);
            if (value != null) {
                _overload.AddReturnValue(value);
            }
            return true; // We want to evaluate all code so all private variables in __new__ get defined
        }

        // Classes and functions are walked by their respective evaluators
        public override bool Walk(ClassDefinition node) => false;
        public override bool Walk(FunctionDefinition node) {
            // Inner function, declare as variable. Do not set variable location
            // since it is not an assignment visible to the user.
            var m = SymbolTable.Evaluate(node);
            if (m != null) {
                Eval.DeclareVariable(node.NameExpression.Name, m, VariableSource.Declaration, node.NameExpression);
            }
            return false;
        }

        private void DeclareParameters(bool declareVariables) {
            // For class method no need to add extra parameters, but first parameter type should be the class.
            // For static and unbound methods do not add or set anything.
            // For regular bound methods add first parameter and set it to the class.

            var parameters = new List<ParameterInfo>();
            var skip = 0;
            if (_self != null && _function.HasClassFirstArgument()) {
                var p0 = FunctionDefinition.Parameters.FirstOrDefault();
                if (p0 != null && !string.IsNullOrEmpty(p0.Name)) {
                    // Actual parameter type will be determined when method is invoked.
                    // The reason is that if method might be called on a derived class.
                    // Declare self or cls in this scope.
                    if (declareVariables) {
                        Eval.DeclareVariable(p0.Name, new PythonInstance(_self), VariableSource.Declaration, p0.NameExpression);
                    }
                    // Set parameter info.
                    var pi = new ParameterInfo(Ast, p0, _self, null, false);
                    parameters.Add(pi);
                    skip++;
                }
            }

            // Declare parameters in scope
            IMember defaultValue = null;
            for (var i = skip; i < FunctionDefinition.Parameters.Length; i++) {
                var isGeneric = false;
                var p = FunctionDefinition.Parameters[i];
                if (!string.IsNullOrEmpty(p.Name)) {
                    IPythonType paramType = null;
                    if (p.DefaultValue != null) {
                        defaultValue = Eval.GetValueFromExpression(p.DefaultValue);
                        // If parameter has default value, look for the annotation locally first
                        // since outer type may be getting redefined. Consider 's = None; def f(s: s = 123): ...
                        paramType = Eval.GetTypeFromAnnotation(p.Annotation, out isGeneric, LookupOptions.Local | LookupOptions.Builtins);
                        // Default value of None does not mean the parameter is None, just says it can be missing.
                        defaultValue = defaultValue.IsUnknown() || defaultValue.IsOfType(BuiltinTypeId.NoneType) ? null : defaultValue;
                        if (paramType == null && defaultValue != null) {
                            paramType = defaultValue.GetPythonType();
                        }
                    }
                    // If all else fails, look up globally.
                    paramType = paramType ?? Eval.GetTypeFromAnnotation(p.Annotation, out isGeneric) ?? Eval.UnknownType;
                    var pi = new ParameterInfo(Ast, p, paramType, defaultValue, isGeneric);
                    if (declareVariables) {
                        DeclareParameter(p, pi);
                    }
                    parameters.Add(pi);
                }
            }
            _overload.SetParameters(parameters);
        }

        private void DeclareParameter(Parameter p, ParameterInfo pi) {
            IPythonType paramType;
            // If type is known from annotation, use it.
            if (pi != null && !pi.Type.IsUnknown() && !pi.Type.IsGenericParameter()) {
                // TODO: technically generics may have constraints. Should we consider them?
                paramType = pi.Type;
            } else {
                paramType = pi?.DefaultValue?.GetPythonType() ?? Eval.UnknownType;
            }
            Eval.DeclareVariable(p.Name, new PythonInstance(paramType), VariableSource.Declaration, p.NameExpression);
        }
    }
}
