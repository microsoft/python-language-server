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
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Analysis.Values.Collections;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;
using ErrorCodes = Microsoft.Python.Analysis.Diagnostics.ErrorCodes;

namespace Microsoft.Python.Analysis.Analyzer.Symbols {
    [DebuggerDisplay("{FunctionDefinition.Name}")]
    internal sealed class FunctionEvaluator : MemberEvaluator {
        private readonly IPythonClassMember _function;
        private readonly PythonFunctionOverload _overload;
        private readonly IPythonClassType _self;

        public FunctionEvaluator(ExpressionEval eval, PythonFunctionOverload overload)
            : base(eval, overload.FunctionDefinition) {
            _overload = overload;
            _function = overload.ClassMember ?? throw new NullReferenceException(nameof(overload.ClassMember));
            _self = _function.DeclaringType as PythonClassType;

            FunctionDefinition = overload.FunctionDefinition;
        }

        private FunctionDefinition FunctionDefinition { get; }

        public override void Evaluate() {
            var stub = SymbolTable.ReplacedByStubs.Contains(Target)
                       || _function.DeclaringModule.ModuleType == ModuleType.Stub
                       || Module.ModuleType == ModuleType.Specialized;

            using (Eval.OpenScope(_function.DeclaringModule, FunctionDefinition, out _)) {
                var returnType = TryDetermineReturnValue();

                var parameters = Eval.CreateFunctionParameters(_self, _function, FunctionDefinition, !stub);
                CheckValidOverload(parameters);
                _overload.SetParameters(parameters);

                // Do process body of constructors since they may be declaring
                // variables that are later used to determine return type of other
                // methods and properties.
                var ctor = _function.IsDunderInit() || _function.IsDunderNew();
                if (ctor || returnType.IsUnknown() || Module.ModuleType == ModuleType.User) {
                    // Return type from the annotation is sufficient for libraries and stubs, no need to walk the body.
                    FunctionDefinition.Body?.Walk(this);
                    // For libraries remove declared local function variables to free up some memory
                    // unless function has inner classes or functions.
                    var optionsProvider = Eval.Services.GetService<IAnalysisOptionsProvider>();
                    if (Module.ModuleType != ModuleType.User &&
                        optionsProvider?.Options.KeepLibraryLocalVariables != true &&
                        Eval.CurrentScope.Variables.All(
                            v => v.GetPythonType<IPythonClassType>() == null &&
                                 v.GetPythonType<IPythonFunctionType>() == null)
                        ) {
                        ((VariableCollection)Eval.CurrentScope.Variables).Clear();
                    }
                }
            }
            Result = _function;
        }

        private IPythonType TryDetermineReturnValue() {
            var annotationType = Eval.GetTypeFromAnnotation(FunctionDefinition.ReturnAnnotation, LookupOptions.All);
            if (!annotationType.IsUnknown()) {
                // Annotations are typically types while actually functions return
                // instances unless specifically annotated to a type such as Type[T].
                // TODO: try constructing argument set from types. Consider Tuple[_T1, _T2] where _T1 = TypeVar('_T1', str, bytes)
                var t = annotationType.CreateInstance(ArgumentSet.Empty(FunctionDefinition.ReturnAnnotation, Eval));
                // If instance could not be created, such as when return type is List[T] and
                // type of T is not yet known, just use the type.
                var instance = t.IsUnknown() ? (IMember)annotationType : t;
                _overload.SetReturnValue(instance, true); _overload.SetReturnValue(instance, true);
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
            return annotationType;
        }

        private void CheckValidOverload(IReadOnlyList<IParameterInfo> parameters) {
            if (_self?.MemberType == PythonMemberType.Class) {
                switch (_function) {
                    case IPythonFunctionType function:
                        CheckValidFunction(function, parameters);
                        break;
                        //TODO check properties
                }
            }
        }

        private void CheckValidFunction(IPythonFunctionType function, IReadOnlyList<IParameterInfo> parameters) {
            // Don't give diagnostics on functions defined in metaclasses
            if (_self.IsMetaclass()) {
                return;
            }

            // Static methods don't need any diagnostics
            if (function.IsStatic) {
                return;
            }

            // Lambdas never get a self/cls argument.
            if (function.IsLambda()) {
                return;
            }

            // Otherwise, functions defined in classes must have at least one argument
            if (parameters.IsNullOrEmpty()) {
                var funcLoc = Eval.GetLocation(FunctionDefinition.NameExpression);
                ReportFunctionParams(Resources.NoMethodArgument, ErrorCodes.NoMethodArgument, funcLoc);
                return;
            }

            var param = parameters[0].Name;
            var paramLoc = Eval.GetLocation(FunctionDefinition.Parameters[0]);
            // If it is a class method check for cls
            if (function.IsClassMethod && !param.Equals("cls")) {
                ReportFunctionParams(Resources.NoClsArgument, ErrorCodes.NoClsArgument, paramLoc);
            }

            // If it is a method check for self
            if (!function.IsClassMethod && !param.Equals("self")) {
                ReportFunctionParams(Resources.NoSelfArgument, ErrorCodes.NoSelfArgument, paramLoc);
            }
        }

        private void ReportFunctionParams(string message, string errorCode, LocationInfo location) {
            Eval.ReportDiagnostics(
                Eval.Module.Uri,
                new DiagnosticsEntry(
                    message.FormatInvariant(FunctionDefinition.Name),
                    location.Span,
                    errorCode,
                    Parsing.Severity.Warning,
                    DiagnosticSource.Analysis));
        }

        public override bool Walk(AssignmentStatement node) {
            var value = Eval.GetValueFromExpression(node.Right) ?? Eval.UnknownType;
            foreach (var lhs in node.Left) {
                switch (lhs) {
                    case MemberExpression memberExp when memberExp.Target is NameExpression nameExp1:
                        if (_function.DeclaringType.GetPythonType() is PythonClassType t && nameExp1.Name == "self") {
                            t.AddMembers(new[] { new KeyValuePair<string, IMember>(memberExp.Name, value) }, false);
                        }
                        continue;
                    case NameExpression nameExp2 when nameExp2.Name == "self":
                        // Only assign to 'self' if it is not declared yet.
                        if (Eval.LookupNameInScopes(nameExp2.Name, out _) == null) {
                            Eval.DeclareVariable(nameExp2.Name, value, VariableSource.Declaration);
                        }
                        return true;
                }
            }
            return base.Walk(node);
        }

        public override bool Walk(ReturnStatement node) {
            var value = Eval.GetValueFromExpression(node.Expression);
            if (value != null) {

                // although technically legal, __init__ in a constructor should not have a not-none return value
                if (_function.IsDunderInit() && !value.IsOfType(BuiltinTypeId.None)) {
                    Eval.ReportDiagnostics(Module.Uri, new DiagnosticsEntry(
                            Resources.ReturnInInit,
                            node.GetLocation(Eval).Span,
                            ErrorCodes.ReturnInInit,
                            Severity.Warning,
                            DiagnosticSource.Analysis));
                }

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
    }
}
