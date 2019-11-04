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
using Microsoft.Python.Analysis.Analyzer.Evaluation;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Core.OS;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Symbols {
    /// <summary>
    /// Walks module AST and collect all classes, functions and method
    /// so the symbol table can resolve references on demand.
    /// </summary>
    internal sealed class SymbolCollector : PythonWalker {
        private readonly Dictionary<ScopeStatement, PythonType> _typeMap = new Dictionary<ScopeStatement, PythonType>();
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

        public override bool Walk(IfStatement node)
            => node.WalkIfWithSystemConditions(this, _eval.Ast.LanguageVersion, _eval.Services.GetService<IOSPlatform>());

        public override bool Walk(ClassDefinition cd) {
            if (IsDeprecated(cd)) {
                return false;
            }

            if (!string.IsNullOrEmpty(cd.NameExpression?.Name)) {
                var classInfo = CreateClass(cd);
                // The variable is transient (non-user declared) hence it does not have location.
                // Class type is tracking locations for references and renaming.
                _eval.DeclareVariable(cd.Name, classInfo, VariableSource.Declaration);
                _table.Add(new ClassEvaluator(_eval, cd));
                // Open class scope
                _scopes.Push(_eval.OpenScope(_eval.Module, cd, out _));
            }
            return true;
        }

        public override void PostWalk(ClassDefinition cd) {
            if (!IsDeprecated(cd) && !string.IsNullOrEmpty(cd.NameExpression?.Name)) {
                _scopes.Pop().Dispose();
            }
            base.PostWalk(cd);
        }

        public override bool Walk(FunctionDefinition fd) {
            if (IsDeprecated(fd)) {
                return false;
            }
            if (!string.IsNullOrEmpty(fd.Name)) {
                AddFunctionOrProperty(fd);
                // Open function scope
                _scopes.Push(_eval.OpenScope(_eval.Module, fd, out _));
            }
            return true;
        }

        public override void PostWalk(FunctionDefinition fd) {
            if (!IsDeprecated(fd) && !string.IsNullOrEmpty(fd.Name)) {
                _scopes.Pop().Dispose();
            }
            base.PostWalk(fd);
        }

        private PythonClassType CreateClass(ClassDefinition cd) {
            PythonType declaringType = null;
            if(!(cd.Parent is PythonAst)) {
                Debug.Assert(_typeMap.ContainsKey(cd.Parent));
                _typeMap.TryGetValue(cd.Parent, out declaringType);
            }
            var cls = new PythonClassType(cd, declaringType, _eval.GetLocationOfName(cd),
                _eval.SuppressBuiltinLookup ? BuiltinTypeId.Unknown : BuiltinTypeId.Type);
            _typeMap[cd] = cls;

            declaringType?.AddMember(cls.Name, cls, overwrite: true);
            return cls;
        }


        private void AddFunctionOrProperty(FunctionDefinition fd) {
            var declaringType = fd.Parent != null && _typeMap.TryGetValue(fd.Parent, out var t) ? t : null;
            if (!TryAddProperty(fd, declaringType)) {
                AddFunction(fd, declaringType);
            }
        }

        private void AddFunction(FunctionDefinition fd, PythonType declaringType) {
            if (!(_eval.LookupNameInScopes(fd.Name, LookupOptions.Local) is PythonFunctionType f)) {
                f = new PythonFunctionType(fd, declaringType, _eval.GetLocationOfName(fd));
                // The variable is transient (non-user declared) hence it does not have location.
                // Function type is tracking locations for references and renaming.
                _eval.DeclareVariable(fd.Name, f, VariableSource.Declaration);
                _typeMap[fd] = f;
                declaringType?.AddMember(f.Name, f, overwrite: true);
            }
            AddOverload(fd, f, o => f.AddOverload(o));
        }

        private void AddOverload(FunctionDefinition fd, IPythonClassMember function, Action<PythonFunctionOverload> addOverload) {
            // Check if function exists in stubs. If so, take overload from stub
            // and the documentation from this actual module.
            if (!_table.ReplacedByStubs.Contains(fd)) {
                var stubOverload = GetOverloadFromStub(fd);
                if (stubOverload != null) {
                    var documentation = fd.GetDocumentation();
                    if (!string.IsNullOrEmpty(documentation)) {
                        stubOverload.SetDocumentation(documentation);
                    }
                    addOverload(stubOverload);
                    _table.ReplacedByStubs.Add(fd);
                    return;
                }
            }

            if (!_table.Contains(fd)) {
                // Do not evaluate parameter types just yet. During light-weight top-level information
                // collection types cannot be determined as imports haven't been processed.
                var overload = new PythonFunctionOverload(function, fd, _eval.GetLocationOfName(fd), fd.ReturnAnnotation?.ToCodeString(_eval.Ast));
                addOverload(overload);
                _table.Add(new FunctionEvaluator(_eval, overload));
            }
        }

        private PythonFunctionOverload GetOverloadFromStub(FunctionDefinition node) {
            var t = GetMemberFromStub(node.Name).GetPythonType();
            if (t is IPythonFunctionType f) {
                return f.Overloads
                    .OfType<PythonFunctionOverload>()
                    .FirstOrDefault(o => o.Parameters.Count == node.Parameters.Where(p => !p.IsPositionalOnlyMarker).Count());
            }
            return null;
        }

        private bool TryAddProperty(FunctionDefinition node, PythonType declaringType) {
            // We can't add a property to an unknown type. Fallback to a regular function for now.
            // TOOD: Decouple declaring types from the property.
            if (declaringType.IsUnknown()) {
                return false;
            }

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

        private void AddProperty(FunctionDefinition fd, PythonType declaringType, bool isAbstract) {
            if (!(_eval.LookupNameInScopes(fd.Name, LookupOptions.Local) is PythonPropertyType p)) {
                p = new PythonPropertyType(fd, _eval.GetLocationOfName(fd), declaringType, isAbstract);
                // The variable is transient (non-user declared) hence it does not have location.
                // Property type is tracking locations for references and renaming.
                _eval.DeclareVariable(fd.Name, p, VariableSource.Declaration);
                _typeMap[fd] = p;
                declaringType?.AddMember(p.Name, p, overwrite: true);
            }
            AddOverload(fd, p, o => p.AddOverload(o));
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

        private static bool IsDeprecated(ClassDefinition cd)
            => cd.Decorators?.Decorators != null && IsDeprecated(cd.Decorators.Decorators);

        private static bool IsDeprecated(FunctionDefinition fd)
            => fd.Decorators?.Decorators != null && IsDeprecated(fd.Decorators.Decorators);

        private static bool IsDeprecated(IEnumerable<Expression> decorators)
            => decorators.OfType<CallExpression>().Any(IsDeprecationDecorator);

        private static bool IsDeprecationDecorator(CallExpression c)
            => (c.Target is MemberExpression n1 && n1.Name == "deprecated") ||
               (c.Target is NameExpression n2 && n2.Name == "deprecated");
    }
}
