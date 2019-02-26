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
using System.Diagnostics;
using System.Linq;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Disposables;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Evaluation {
    internal sealed partial class ExpressionEval {
        public IMember GetInScope(string name, IScope scope)
            => scope.Variables.TryGetVariable(name, out var variable) ? variable.Value : null;

        public T GetInScope<T>(string name, IScope scope) where T : class, IMember
            => scope.Variables.TryGetVariable(name, out var variable) ? variable.Value as T : null;

        public IMember GetInScope(string name) => GetInScope(name, CurrentScope);
        public T GetInScope<T>(string name) where T : class, IMember => GetInScope<T>(name, CurrentScope);

        public void DeclareVariable(string name, IMember value, VariableSource source, Node expression)
            => DeclareVariable(name, value, source, GetLoc(expression));

        public void DeclareVariable(string name, IMember value, VariableSource source, LocationInfo location, bool overwrite = false) {
            var member = GetInScope(name);
            if (member != null) {
                if (!value.IsUnknown()) {
                    CurrentScope.DeclareVariable(name, value, source, location);
                }
            } else {
                CurrentScope.DeclareVariable(name, value, source, location);
            }
        }

        [DebuggerStepThrough]
        public IMember LookupNameInScopes(string name, out IScope scope) => LookupNameInScopes(name, out scope, DefaultLookupOptions);

        [DebuggerStepThrough]
        public IMember LookupNameInScopes(string name, LookupOptions options) => LookupNameInScopes(name, out _, options);

        public IMember LookupNameInScopes(string name, out IScope scope, LookupOptions options) {
            var scopes = CurrentScope.ToChainTowardsGlobal().ToList();
            if (scopes.Count == 1) {
                if (!options.HasFlag(LookupOptions.Local) && !options.HasFlag(LookupOptions.Global)) {
                    scopes.Clear();
                }
            } else if (scopes.Count >= 2) {
                if (!options.HasFlag(LookupOptions.Nonlocal)) {
                    while (scopes.Count > 2) {
                        scopes.RemoveAt(1);
                    }
                }

                if (!options.HasFlag(LookupOptions.Local)) {
                    scopes.RemoveAt(0);
                }

                if (!options.HasFlag(LookupOptions.Global)) {
                    scopes.RemoveAt(scopes.Count - 1);
                }
            }

            scope = scopes.FirstOrDefault(s => s.Variables.Contains(name));
            var value = scope?.Variables[name].Value;
            if (value == null) {
                var builtins = Interpreter.ModuleResolution.BuiltinsModule;
                value = Interpreter.ModuleResolution.BuiltinsModule.GetMember(name);
                if (Module != builtins && options.HasFlag(LookupOptions.Builtins)) {
                    value = builtins.GetMember(name);
                    scope = builtins.GlobalScope;
                }
            }

            return value;
        }

        public IPythonType GetTypeFromAnnotation(Expression expr, LookupOptions options = LookupOptions.Global | LookupOptions.Builtins)
            => GetTypeFromAnnotation(expr, out _, options);

        public IPythonType GetTypeFromAnnotation(Expression expr, out bool isGeneric, LookupOptions options = LookupOptions.Global | LookupOptions.Builtins) {
            isGeneric = false;
            switch (expr) {
                case null:
                    return null;
                case CallExpression callExpr:
                    // x: NamedTuple(...)
                    return GetValueFromCallable(callExpr)?.GetPythonType() ?? UnknownType;
                case IndexExpression indexExpr:
                    // Try generics
                    var target = GetValueFromExpression(indexExpr.Target);
                    var result = GetValueFromGeneric(target, indexExpr);
                    if (result != null) {
                        isGeneric = true;
                        return result.GetPythonType();
                    }
                    break;
            }

            // Look at specialization and typing first
            var ann = new TypeAnnotation(Ast.LanguageVersion, expr);
            return ann.GetValue(new TypeAnnotationConverter(this, options));
        }

        /// <summary>
        /// Locates and opens existing scope for a node or creates a new scope
        /// as a child of the specified scope. Scope is pushed on the stack
        /// and will be removed when returned the disposable is disposed.
        /// </summary>
        public IDisposable OpenScope(IPythonModule module, ScopeStatement node, out Scope fromScope) {
            fromScope = null;
            if (node == null) {
                return Disposable.Empty;
            }

            // During analysis module global scope has not changed yet since it updates
            // When the analysis completed. Therefore if module is the one we are
            // analyzing, use scope from the evaluator rather than from the module.
            var gs = Module.Equals(module) || module == null ? GlobalScope : module.GlobalScope as Scope;
            if (gs == null) {
                return Disposable.Empty;
            }

            if (node.Parent != null) {
                fromScope = gs
                    .TraverseBreadthFirst(s => s.Children.OfType<Scope>())
                    .FirstOrDefault(s => s.Node == node.Parent);
            }

            fromScope = fromScope ?? gs;
            if (fromScope != null) {
                var scope = fromScope.Children.OfType<Scope>().FirstOrDefault(s => s.Node == node);
                if (scope == null) {
                    scope = new Scope(node, fromScope, true);
                    fromScope.AddChildScope(scope);
                }

                _openScopes.Push(scope);
                CurrentScope = scope;
            }
            return new ScopeTracker(this);
        }

        private class ScopeTracker : IDisposable {
            private readonly ExpressionEval _eval;

            public ScopeTracker(ExpressionEval eval) {
                _eval = eval;
            }

            public void Dispose() {
                Debug.Assert(_eval._openScopes.Count > 0, "Attempt to close global scope");
                _eval._openScopes.Pop();
                _eval.CurrentScope = _eval._openScopes.Count == 0 ? _eval.GlobalScope : _eval._openScopes.Peek();
            }
        }
    }
}
