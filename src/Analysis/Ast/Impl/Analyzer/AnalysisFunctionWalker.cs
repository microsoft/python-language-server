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
using Microsoft.Python.Analysis.Extensions;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    [DebuggerDisplay("{Target.Name}")]
    internal sealed class AnalysisFunctionWalker : AnalysisWalker {
        private readonly Scope _parentScope;
        private readonly PythonFunctionOverload _overload;
        private readonly IPythonClassMember _function;
        private IPythonClass _self;

        public AnalysisFunctionWalker(
            ExpressionLookup lookup,
            FunctionDefinition targetFunction,
            PythonFunctionOverload overload,
            IPythonClassMember function
        ) : base(lookup) {
            Target = targetFunction ?? throw new ArgumentNullException(nameof(targetFunction));
            _overload = overload ?? throw new ArgumentNullException(nameof(overload));
            _function = function ?? throw new ArgumentNullException(nameof(function));
            _parentScope = Lookup.CurrentScope;
        }

        public FunctionDefinition Target { get; }

        public async Task WalkAsync(CancellationToken cancellationToken = default) {
            using (Lookup.OpenScope(_parentScope)) {
                _self = Lookup.LookupNameInScopes("__class__", ExpressionLookup.LookupOptions.Local) as IPythonClass;
                using (Lookup.CreateScope(Target, _parentScope)) {

                    var annotationType = Lookup.GetTypeFromAnnotation(Target.ReturnAnnotation);
                    if (!annotationType.IsUnknown()) {
                        _overload.SetReturnValue(annotationType, true);
                    }

                    await DeclareParametersAsync(cancellationToken);
                    if (_overload.Documentation == null) {
                        var docNode = (Target.Body as SuiteStatement)?.Statements.FirstOrDefault();
                        var ce = (docNode as ExpressionStatement)?.Expression as ConstantExpression;
                        if (ce?.Value is string doc) {
                            _overload.SetDocumentation(doc);
                        }
                    }

                    if (annotationType.IsUnknown() || Module.ModuleType == ModuleType.User) {
                        // Return type from the annotation is sufficient for libraries
                        // and stubs, no need to walk the body.
                        if (Target.Body != null) {
                            await Target.Body.WalkAsync(this, cancellationToken);
                        }
                    }
                } // Function scope
            } // Restore original scope at the entry
        }

        public override async Task<bool> WalkAsync(AssignmentStatement node, CancellationToken cancellationToken = default) {
            var value = await Lookup.GetValueFromExpressionAsync(node.Right, cancellationToken);

            foreach (var lhs in node.Left) {
                if (lhs is MemberExpression memberExp && memberExp.Target is NameExpression nameExp1) {
                    if (_self.GetPythonType() is PythonClass t && nameExp1.Name == "self") {
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
            var value = await Lookup.GetValueFromExpressionAsync(node.Expression, cancellationToken);
            if (value != null) {
                _overload.AddReturnValue(value);
            }
            return true; // We want to evaluate all code so all private variables in __new__ get defined
        }

        public override Task<bool> WalkAsync(ClassDefinition node, CancellationToken cancellationToken = default) {
            // TODO: report that classes are not supposed to appear inside functions.
            return Task.FromResult(false);
        }

        private async Task DeclareParametersAsync(CancellationToken cancellationToken = default) {
            // For class method no need to add extra parameters, but first parameter type should be the class.
            // For static and unbound methods do not add or set anything.
            // For regular bound methods add first parameter and set it to the class.

            var skip = 0;
            if (_self != null && _function.HasClassFirstArgument()) {
                var p0 = Target.Parameters.FirstOrDefault();
                if (p0 != null && !string.IsNullOrEmpty(p0.Name)) {
                    if (_overload.Parameters.Count > 0 && _overload.Parameters[0] is ParameterInfo pi) {
                        // TODO: set instance vs class type info for regular methods.
                        Lookup.DeclareVariable(p0.Name, _self, p0.NameExpression);
                        pi.SetType(_self);
                        skip++;
                    }
                }
            }

            // Declare parameters in scope
            var parameterCount = Math.Min(Target.Parameters.Length, _overload.Parameters.Count);
            for (var i = skip; i < parameterCount; i++) {
                cancellationToken.ThrowIfCancellationRequested();

                var p = Target.Parameters[i];
                var pi = _overload.Parameters[i] as ParameterInfo;

                if (!string.IsNullOrEmpty(p.Name)) {
                    await DeclareParameterAsync(p, i, pi, cancellationToken);
                }
            }
        }

        private async Task DeclareParameterAsync(Parameter p, int index, ParameterInfo pi, CancellationToken cancellationToken = default) {
            var defaultValue = await Lookup.GetValueFromExpressionAsync(p.DefaultValue, cancellationToken) ?? Lookup.UnknownType;

            var defaultValueType = defaultValue.GetPythonType();
            var argType = new CallableArgumentType(index, defaultValueType);

            Lookup.DeclareVariable(p.Name, argType, p.NameExpression);
            pi?.SetDefaultValueType(defaultValueType);
        }
    }
}
