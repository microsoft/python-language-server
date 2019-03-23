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
using System.Linq;
using Microsoft.Python.Analysis.Analyzer.Evaluation;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Symbols {
    /// <summary>
    /// Walks module AST and collect all classes, functions and method
    /// so the symbol table can resolve references on demand.
    /// </summary>
    internal sealed class SymbolCollector : PythonWalker {
        private readonly Dictionary<ScopeStatement, IPythonType> _typeMap = new Dictionary<ScopeStatement, IPythonType>();
        private readonly Stack<IDisposable> _scopes = new Stack<IDisposable>();
        private readonly ModuleSymbolTable _table;
        private readonly ExpressionEval _eval;

        public static void CollectSymbols(ModuleSymbolTable table, ExpressionEval eval) {
            var symbolCollector = new SymbolCollector(table, eval);
            symbolCollector.Walk();
        }

        private SymbolCollector(ModuleSymbolTable table, ExpressionEval eval) {
            _table = table;
            _eval = eval;
        }

        private void Walk() => _eval.Ast.Walk(this);

        public override bool Walk(ClassDefinition cd) {
            if (!string.IsNullOrEmpty(cd.NameExpression?.Name)) {
                var classInfo = CreateClass(cd);
                _eval.DeclareVariable(cd.Name, classInfo, VariableSource.Declaration);
                _table.Add(new ClassEvaluator(_eval, cd));
                // Open class scope
                _scopes.Push(_eval.OpenScope(_eval.Module, cd, out _));
            }
            return true;
        }

        public override void PostWalk(ClassDefinition cd) {
            if (!string.IsNullOrEmpty(cd.NameExpression?.Name)) {
                _scopes.Pop().Dispose();
            }
            base.PostWalk(cd);
        }

        public override bool Walk(FunctionDefinition fd) {
            if (!string.IsNullOrEmpty(fd.NameExpression?.Name)) {
                AddFunctionOrProperty(fd);
                // Open function scope
                _scopes.Push(_eval.OpenScope(_eval.Module, fd, out _));
            }
            return true;
        }

        public override void PostWalk(FunctionDefinition fd) {
            if (!string.IsNullOrEmpty(fd.NameExpression?.Name)) {
                _scopes.Pop().Dispose();
            }
            base.PostWalk(fd);
        }

        private PythonClassType CreateClass(ClassDefinition node) {
            var cls = new PythonClassType(node, _eval.Module, 
                _eval.SuppressBuiltinLookup ? BuiltinTypeId.Unknown : BuiltinTypeId.Type);
            _typeMap[node] = cls;
            return cls;
        }


        private void AddFunctionOrProperty(FunctionDefinition fd) {
            var declaringType = fd.Parent != null && _typeMap.TryGetValue(fd.Parent, out var t) ? t : null;
            if (!TryAddProperty(fd, declaringType)) {
                AddFunction(fd, declaringType);
            }
        }

        private IMember AddFunction(FunctionDefinition node, IPythonType declaringType) {
            if (!(_eval.LookupNameInScopes(node.Name, LookupOptions.Local) is PythonFunctionType existing)) {
                existing = new PythonFunctionType(node, _eval.Module, declaringType);
                _eval.DeclareVariable(node.Name, existing, VariableSource.Declaration);
            }
            AddOverload(node, existing, o => existing.AddOverload(o));
            return existing;
        }

        private void AddOverload(FunctionDefinition node, IPythonClassMember function, Action<IPythonFunctionOverload> addOverload) {
            // Check if function exists in stubs. If so, take overload from stub
            // and the documentation from this actual module.
            if (!_table.ReplacedByStubs.Contains(node)) {
                var stubOverload = GetOverloadFromStub(node);
                if (stubOverload != null) {
                    if (!string.IsNullOrEmpty(node.GetDocumentation())) {
                        stubOverload.SetDocumentationProvider(_ => node.GetDocumentation());
                    }
                    addOverload(stubOverload);
                    _table.ReplacedByStubs.Add(node);
                    return;
                }
            }

            if (!_table.Contains(node)) {
                // Do not evaluate parameter types just yet. During light-weight top-level information
                // collection types cannot be determined as imports haven't been processed.
                var location = _eval.GetLocOfName(node, node.NameExpression);
                var overload = new PythonFunctionOverload(node, function, _eval.Module);
                addOverload(overload);
                _table.Add(new FunctionEvaluator(_eval, node, overload, function));
            }
        }

        private PythonFunctionOverload GetOverloadFromStub(FunctionDefinition node) {
            var t = GetMemberFromStub(node.Name).GetPythonType();
            if (t is IPythonFunctionType f) {
                return f.Overloads
                    .OfType<PythonFunctionOverload>()
                    .FirstOrDefault(o => o.Parameters.Count == node.Parameters.Length);
            }
            return null;
        }

        private bool TryAddProperty(FunctionDefinition node, IPythonType declaringType) {
            var dec = node.Decorators?.Decorators;
            var decorators = dec != null ? dec.ExcludeDefault().ToArray() : Array.Empty<Expression>();

            foreach (var d in decorators.OfType<NameExpression>()) {
                switch (d.Name) {
                    case @"property":
                        AddProperty(node, declaringType, false);
                        return true;
                    case @"abstractproperty":
                        AddProperty(node, declaringType, true);
                        return true;
                }
            }
            return false;
        }

        private PythonPropertyType AddProperty(FunctionDefinition node, IPythonType declaringType, bool isAbstract) {
            if (!(_eval.LookupNameInScopes(node.Name, LookupOptions.Local) is PythonPropertyType existing)) {
                existing = new PythonPropertyType(node, _eval.Module, declaringType, isAbstract);
                _eval.DeclareVariable(node.Name, existing, VariableSource.Declaration);
            }
            AddOverload(node, existing, o => existing.AddOverload(o));
            return existing;
        }

        private IMember GetMemberFromStub(string name) {
            if (_eval.Module.Stub == null) {
                return null;
            }

            var memberNameChain = new List<string>(Enumerable.Repeat(name, 1));
            IScope scope = _eval.CurrentScope;

            while (scope != _eval.GlobalScope) {
                memberNameChain.Add(scope.Name);
                scope = scope.OuterScope;
            }

            IMember member = _eval.Module.Stub;
            for (var i = memberNameChain.Count - 1; i >= 0; i--) {
                if (!(member is IMemberContainer mc)) {
                    return null;
                }
                member = mc.GetMember(memberNameChain[i]);
                if (member == null) {
                    return null;
                }
            }

            return member;
        }
    }
}
