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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

#if NOT_YET
namespace Microsoft.Python.Analysis.Analyzer.Evaluation {
    internal sealed class FunctionCallEvaluator: AnalysisWalker {
        private readonly ExpressionEval _eval;
        private readonly PythonFunctionOverload _overload;
        private readonly IPythonType _declaringType;
        private readonly IPythonClassType _self;
        private IMember _result;

        public FunctionCallEvaluator(ExpressionEval eval, PythonFunctionOverload overload, IPythonType declaringType): base(eval) {
            _eval = _eval ?? throw new ArgumentNullException(nameof(eval));
            _overload = overload ?? throw new ArgumentNullException(nameof(overload));
            _declaringType = declaringType;
            _self = _declaringType as PythonClassType;
        }

        public async Task<IMember> EvaluateCallAsync(IReadOnlyList<IMember> args, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();

            // Open scope and declare parameters
            using (_eval.OpenScope(_overload.FunctionDefinition, out _)) {
                await DeclareParametersAsync(cancellationToken);
                await _overload.FunctionDefinition.Body.WalkAsync(this, cancellationToken);
            }
            return _result;
        }

        public override async Task<bool> WalkAsync(AssignmentStatement node, CancellationToken cancellationToken = default) {
            var value = await Eval.GetValueFromExpressionAsync(node.Right, cancellationToken);

            foreach (var lhs in node.Left) {
                if (lhs is MemberExpression memberExp && memberExp.Target is NameExpression nameExp1) {
                    if (_declaringType.GetPythonType() is PythonClassType t && nameExp1.Name == "self") {
                        t.AddMembers(new[] { new KeyValuePair<string, IMember>(memberExp.Name, value) }, true);
                    }
                    continue;
                }

                if (lhs is NameExpression nameExp2 && nameExp2.Name == "self") {
                    return true; // Don't assign to 'self'
                }
            }
            return await base.WalkAsync(node, cancellationToken);
        }

        public override async Task<bool> WalkAsync(ReturnStatement node, CancellationToken cancellationToken = default) {
            var value = await Eval.GetValueFromExpressionAsync(node.Expression, cancellationToken);
            if (!value.IsUnknown()) {
                _result = value;
                return false;
            }
            return true;
        }

        // Classes and functions are walked by their respective evaluators
        public override async Task<bool> WalkAsync(ClassDefinition node, CancellationToken cancellationToken = default) {
            await SymbolTable.EvaluateAsync(node, cancellationToken);
            return false;
        }

        public override async Task<bool> WalkAsync(FunctionDefinition node, CancellationToken cancellationToken = default) {
            await SymbolTable.EvaluateAsync(node, cancellationToken);
            return false;
        }

        private async Task DeclareParametersAsync(CancellationToken cancellationToken = default) {
            // For class method no need to add extra parameters, but first parameter type should be the class.
            // For static and unbound methods do not add or set anything.
            // For regular bound methods add first parameter and set it to the class.

            var parameters = new List<ParameterInfo>();
            var skip = 0;
            //if (_self != null && _function.HasClassFirstArgument()) {
            //    var p0 = FunctionDefinition.Parameters.FirstOrDefault();
            //    if (p0 != null && !string.IsNullOrEmpty(p0.Name)) {
            //        // Actual parameter type will be determined when method is invoked.
            //        // The reason is that if method might be called on a derived class.
            //        var selfType = new FunctionArgumentType(0, _self);
            //        // Declare self or cls in this scope.
            //        Eval.DeclareVariable(p0.Name, selfType, p0.NameExpression);
            //        // Set parameter info.
            //        var pi = new ParameterInfo(Ast, p0, selfType);
            //        pi.SetType(selfType);
            //        parameters.Add(pi);
            //        skip++;
            //    }
            //}

            //// Declare parameters in scope
            //for (var i = skip; i < FunctionDefinition.Parameters.Length; i++) {
            //    cancellationToken.ThrowIfCancellationRequested();

            //    var p = FunctionDefinition.Parameters[i];
            //    if (!string.IsNullOrEmpty(p.Name)) {
            //        // If parameter has default value, look for the annotation locally first
            //        // since outer type may be getting redefined. Consider 's = None; def f(s: s = 123): ...
            //        IPythonType paramType = null;
            //        if (p.DefaultValue != null) {
            //            paramType = await Eval.GetTypeFromAnnotationAsync(p.Annotation, cancellationToken, LookupOptions.Local | LookupOptions.Builtins);
            //            if (paramType == null) {
            //                var defaultValue = await Eval.GetValueFromExpressionAsync(p.DefaultValue, cancellationToken);
            //                if (!defaultValue.IsUnknown()) {
            //                    paramType = defaultValue.GetPythonType();
            //                }
            //            }
            //        }
            //        // If all else fails, look up globally.
            //        paramType = paramType ?? await Eval.GetTypeFromAnnotationAsync(p.Annotation, cancellationToken);

            //        var pi = new ParameterInfo(Ast, p, paramType);
            //        await DeclareParameterAsync(p, i, pi, cancellationToken);
            //        parameters.Add(pi);
            //    }
            //}
            _overload.SetParameters(parameters);
        }

        private async Task DeclareParameterAsync(Parameter p, int index, ParameterInfo pi, CancellationToken cancellationToken = default) {
            IPythonType paramType;

            // If type is known from annotation, use it.
            if (pi != null && !pi.Type.IsUnknown() && !pi.Type.IsGenericParameter()) {
                // TODO: technically generics may have constraints. Should we consider them?
                paramType = pi.Type;
            } else {
                // Declare as an argument which type is only known at the invocation time.
                var defaultValue = await Eval.GetValueFromExpressionAsync(p.DefaultValue, cancellationToken) ?? Eval.UnknownType;
                var defaultValueType = defaultValue.GetPythonType();

                paramType = new FunctionArgumentType(index, defaultValueType);
                if (!defaultValueType.IsUnknown()) {
                    pi?.SetDefaultValueType(defaultValueType);
                }
            }

            Eval.DeclareVariable(p.Name, paramType, p.NameExpression);
        }

        private async Task EvaluateInnerFunctionsAsync(FunctionDefinition fd, CancellationToken cancellationToken = default) {
            // Do not use foreach since walker list is dynamically modified and walkers are removed
            // after processing. Handle __init__ and __new__ first so class variables are initialized.
            var innerFunctions = SymbolTable.Evaluators
                .Where(kvp => kvp.Key.Parent == fd && (kvp.Key is FunctionDefinition))
                .Select(c => c.Value)
                .ExcludeDefault()
                .ToArray();

            foreach (var c in innerFunctions) {
                await SymbolTable.EvaluateAsync(c, cancellationToken);
            }
        }
    }
}
#endif
