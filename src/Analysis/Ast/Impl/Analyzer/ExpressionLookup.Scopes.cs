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
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    internal sealed partial class ExpressionLookup {
        public IMember GetInScope(string name)
            => CurrentScope.Variables.TryGetVariable(name, out var variable) ? variable.Value : null;

        public T GetInScope<T>(string name) where T : class, IMember
            => CurrentScope.Variables.TryGetVariable(name, out var variable) ? variable.Value as T : null;

        public void DeclareVariable(string name, IMember value, Node expression)
            => DeclareVariable(name, value, GetLoc(expression));

        public void DeclareVariable(string name, IMember value, LocationInfo location, bool overwrite = false) {
            var member = GetInScope(name);
            if (member != null) {
                if (!value.IsUnknown()) {
                    CurrentScope.DeclareVariable(name, value, location);
                }
            } else {
                CurrentScope.DeclareVariable(name, value, location);
            }
        }

        [Flags]
        public enum LookupOptions {
            None = 0,
            Local,
            Nonlocal,
            Global,
            Builtins,
            Normal = Local | Nonlocal | Global | Builtins
        }

        [DebuggerStepThrough]
        public IMember LookupNameInScopes(string name) => LookupNameInScopes(name, DefaultLookupOptions);

        public IMember LookupNameInScopes(string name, LookupOptions options) {
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

            var scope = scopes.FirstOrDefault(s => s.Variables.Contains(name));
            var value = scope?.Variables[name].Value;
            if (value == null) {
                if (Module != Interpreter.ModuleResolution.BuiltinsModule && options.HasFlag(LookupOptions.Builtins)) {
                    value = Interpreter.ModuleResolution.BuiltinsModule.GetMember(name);
                }
            }

            return value;
        }

        public IPythonType GetTypeFromAnnotation(Expression expr) {
            if (expr == null) {
                return null;
            }

            // Look at specialization and typing first
            var ann = new TypeAnnotation(Ast.LanguageVersion, expr);
            return ann.GetValue(new TypeAnnotationConverter(this));
        }

        /// <summary>
        /// Moves current scope to the specified scope.
        /// New scope is pushed on the stack and will be removed 
        /// when returned disposable is disposed.
        /// </summary>
        /// <param name="scope"></param>
        public IDisposable OpenScope(Scope scope) {
            _openScopes.Push(CurrentScope);
            CurrentScope = scope;
            return new ScopeTracker(this);
        }

        /// <summary>
        /// Creates new scope as a child of the specified scope.
        /// New scope is pushed on the stack and will be removed 
        /// when returned disposable is disposed.
        /// </summary>
        public IDisposable CreateScope(Node node, Scope fromScope, bool visibleToChildren = true) {
            var s = new Scope(node, fromScope, visibleToChildren);
            fromScope.AddChildScope(s);
            return OpenScope(s);
        }

        private class ScopeTracker : IDisposable {
            private readonly ExpressionLookup _lookup;

            public ScopeTracker(ExpressionLookup lookup) {
                _lookup = lookup;
            }

            public void Dispose() {
                Debug.Assert(_lookup._openScopes.Count > 0, "Attempt to close global scope");
                _lookup.CurrentScope = _lookup._openScopes.Pop();
            }
        }
    }
}
