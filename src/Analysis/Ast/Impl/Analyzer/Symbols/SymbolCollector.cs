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
using System.Threading;
using System.Threading.Tasks;
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
    internal sealed class SymbolCollector : PythonWalkerAsync {
        private readonly Dictionary<ScopeStatement, IPythonType> _typeMap = new Dictionary<ScopeStatement, IPythonType>();
        private readonly Stack<IDisposable> _scopes = new Stack<IDisposable>();
        private readonly ModuleSymbolTable _table;
        private readonly ExpressionEval _eval;

        public static Task CollectSymbolsAsync(ModuleSymbolTable table, ExpressionEval eval, CancellationToken cancellationToken = default) {
            var symbolCollector = new SymbolCollector(table, eval);
            return symbolCollector.WalkAsync(cancellationToken);
        }

        private SymbolCollector(ModuleSymbolTable table, ExpressionEval eval) {
            _table = table;
            _eval = eval;
        }

        private Task WalkAsync(CancellationToken cancellationToken = default) => _eval.Ast.WalkAsync(this, cancellationToken);

        public override Task<bool> WalkAsync(ClassDefinition cd, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            var classInfo = CreateClass(cd);
            _eval.DeclareVariable(cd.Name, classInfo, VariableSource.Declaration, GetLoc(cd));
            _table.Add(new ClassEvaluator(_eval, cd));
            // Open class scope
            _scopes.Push(_eval.OpenScope(cd, out _));
            return Task.FromResult(true);
        }

        public override Task PostWalkAsync(ClassDefinition cd, CancellationToken cancellationToken = default) {
            _scopes.Pop().Dispose();
            return base.PostWalkAsync(cd, cancellationToken);
        }

        public override Task<bool> WalkAsync(FunctionDefinition fd, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            AddFunctionOrProperty(fd);
            // Open function scope
            _scopes.Push(_eval.OpenScope(fd, out _));
            return Task.FromResult(true);
        }

        public override Task PostWalkAsync(FunctionDefinition fd, CancellationToken cancellationToken = default) {
            _scopes.Pop().Dispose();
            return base.PostWalkAsync(fd, cancellationToken);
        }

        private PythonClassType CreateClass(ClassDefinition node) {
            var cls = new PythonClassType(node, _eval.Module, GetLoc(node), 
                _eval.SuppressBuiltinLookup ? BuiltinTypeId.Unknown : BuiltinTypeId.Type);
            _typeMap[node] = cls;
            return cls;
        }


        private void AddFunctionOrProperty(FunctionDefinition fd) {
            var declaringType = fd.Parent != null && _typeMap.TryGetValue(fd.Parent, out var t) ? t : null;
            var loc = GetLoc(fd);
            if (!TryAddProperty(fd, declaringType, loc)) {
                AddFunction(fd, declaringType, loc);
            }
        }

        private IMember AddFunction(FunctionDefinition node, IPythonType declaringType, LocationInfo loc) {
            if (!(_eval.LookupNameInScopes(node.Name, LookupOptions.Local) is PythonFunctionType existing)) {
                existing = new PythonFunctionType(node, _eval.Module, declaringType, loc);
                _eval.DeclareVariable(node.Name, existing, VariableSource.Declaration, loc);
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
                var returnDoc = node.ReturnAnnotation?.ToCodeString(_eval.Ast);
                var overload = new PythonFunctionOverload(node, function, _eval.Module, location, returnDoc);
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

        private bool TryAddProperty(FunctionDefinition node, IPythonType declaringType, LocationInfo location) {
            var dec = node.Decorators?.Decorators;
            var decorators = dec != null ? dec.ExcludeDefault().ToArray() : Array.Empty<Expression>();

            foreach (var d in decorators.OfType<NameExpression>()) {
                switch (d.Name) {
                    case @"property":
                        AddProperty(node, _eval.Module, declaringType, false, location);
                        return true;
                    case @"abstractproperty":
                        AddProperty(node, _eval.Module, declaringType, true, location);
                        return true;
                }
            }
            return false;
        }

        private PythonPropertyType AddProperty(FunctionDefinition node, IPythonModule declaringModule, IPythonType declaringType, bool isAbstract, LocationInfo loc) {
            if (!(_eval.LookupNameInScopes(node.Name, LookupOptions.Local) is PythonPropertyType existing)) {
                existing = new PythonPropertyType(node, declaringModule, declaringType, isAbstract, loc);
                _eval.DeclareVariable(node.Name, existing, VariableSource.Declaration, loc);
            }
            AddOverload(node, existing, o => existing.AddOverload(o));
            return existing;
        }

        private LocationInfo GetLoc(ClassDefinition node) {
            if (node == null || node.StartIndex >= node.EndIndex) {
                return null;
            }

            var start = node.NameExpression?.GetStart(_eval.Ast) ?? node.GetStart(_eval.Ast);
            var end = node.GetEnd(_eval.Ast);
            return new LocationInfo(_eval.Module.FilePath, _eval.Module.Uri, start.Line, start.Column, end.Line, end.Column);
        }

        private LocationInfo GetLoc(Node node) => _eval.GetLoc(node);

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
