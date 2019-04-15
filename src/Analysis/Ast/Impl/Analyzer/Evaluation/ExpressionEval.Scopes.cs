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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
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

        public void DeclareVariable(string name, IMember value, VariableSource source)
            => DeclareVariable(name, value, source, default(Location));

        public void DeclareVariable(string name, IMember value, VariableSource source, IPythonModule module)
            => DeclareVariable(name, value, source, new Location(module, default));

        public void DeclareVariable(string name, IMember value, VariableSource source, Node location, bool overwrite = false)
            => DeclareVariable(name, value, source, GetLocationOfName(location), overwrite);

        public void DeclareVariable(string name, IMember value, VariableSource source, Location location, bool overwrite = false) {
            if (source == VariableSource.Import && value is IVariable v) {
                CurrentScope.LinkVariable(name, v, location);
                return;
            }
            var member = GetInScope(name);
            if (member != null) {
                if (!value.IsUnknown()) {
                    CurrentScope.DeclareVariable(name, value, source, location);
                }
            } else {
                CurrentScope.DeclareVariable(name, value, source, location);
            }
        }

        public IMember LookupNameInScopes(string name, out IScope scope, out IVariable v, LookupOptions options) {
            scope = null;

            switch (options) {
                case LookupOptions.Normal:
                    // Regular lookup: all scopes and builtins.
                    for (var s = CurrentScope; s != null; s = (Scope)s.OuterScope) {
                        if (s.Variables.Contains(name)) {
                            scope = s;
                            break;
                        }
                    }
                    break;
                case LookupOptions.Global:
                case LookupOptions.Global | LookupOptions.Builtins:
                    // Global scope only.
                    if (GlobalScope.Variables.Contains(name)) {
                        scope = GlobalScope;
                    }
                    break;
                case LookupOptions.Nonlocal:
                case LookupOptions.Nonlocal | LookupOptions.Builtins:
                    // All scopes but current and global ones.
                    for (var s = CurrentScope.OuterScope as Scope; s != null && s != GlobalScope; s = (Scope)s.OuterScope) {
                        if (s.Variables.Contains(name)) {
                            scope = s;
                            break;
                        }
                    }
                    break;
                case LookupOptions.Local:
                case LookupOptions.Local | LookupOptions.Builtins:
                    // Just the current scope
                    if (CurrentScope.Variables.Contains(name)) {
                        scope = CurrentScope;
                    }
                    break;
                default:
                    Debug.Fail("Unsupported name lookup combination");
                    break;
            }

            v = scope?.Variables[name];
            var value = v?.Value;
            if (value == null && options.HasFlag(LookupOptions.Builtins)) {
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
        public IDisposable OpenScope(IPythonModule module, ScopeStatement node, out Scope outerScope) {
            outerScope = null;
            if (node == null) {
                return Disposable.Empty;
            }

            // During analysis module global scope is not available since it updates
            // when the analysis completes. Therefore if module is the one we are
            // analyzing, use scope from the evaluator rather than from the module.
            var gs = Module.Equals(module) || module == null ? GlobalScope : module.GlobalScope as Scope;
            if (gs == null) {
                return Disposable.Empty;
            }

            // Get the outer scope first, it must exist. Use either module
            // or this evaluator if the analysis is still in progress.
            var lookup = Module.Equals(module) || module == null ? this : module.Analysis.ExpressionEvaluator as IScopeLookup;
            outerScope = lookup?.GetScope(node.Parent);
            Debug.Assert(outerScope != null);

            var scope = lookup?.GetScope(node);
            if (scope == null) {
                scope = new Scope(node, outerScope, Module);
                outerScope.AddChildScope(scope);
                lookup.AddScope(scope, node);
            }

            _openScopes.Push(scope);
            CurrentScope = scope;

            return new ScopeTracker(this);
        }

        #region IScopeLookup
        private readonly Dictionary<ScopeStatement, Scope> _scopeLookup = new Dictionary<ScopeStatement, Scope>();

        public void AddScope(Scope scope, ScopeStatement statement) {
            if (statement != null) {
                _scopeLookup[statement] = scope; ;
            }
        }

        public Scope GetScope(ScopeStatement statement) {
            if (statement == null || statement is PythonAst) {
                return GlobalScope;
            }
            return _scopeLookup.TryGetValue(statement, out var s) ? s : null;
        }
        #endregion

        private class ScopeTracker : IDisposable {
            private readonly ExpressionEval _eval;

            public ScopeTracker(ExpressionEval eval) {
                _eval = eval;
            }

            public void Dispose() {
                // in case of quick hovering over various items there may be issues
                // with interleaving Open/Close scope requests which may trigger
                // the AF. They are generally harmless, but we may consider handling
                // them better.
                // TODO: figure out threading/locking for the Open/Close pairs.
                // Debug.Assert(_eval._openScopes.Count > 0, "Attempt to close global scope");
                if (_eval._openScopes.Count > 0) {
                    _eval._openScopes.Pop();
                }
                _eval.CurrentScope = _eval._openScopes.Count == 0 ? _eval.GlobalScope : _eval._openScopes.Peek();
            }
        }
    }
}
