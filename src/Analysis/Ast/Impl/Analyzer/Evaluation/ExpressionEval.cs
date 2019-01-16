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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Analyzer.Symbols;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Evaluation {
    /// <summary>
    /// Helper class that provides methods for looking up variables
    /// and types in a chain of scopes during analysis.
    /// </summary>
    internal sealed partial class ExpressionEval {
        private readonly Stack<Scope> _openScopes = new Stack<Scope>();
        private readonly IDiagnosticsService _diagnostics;

        public ExpressionEval(IServiceContainer services, IPythonModule module, PythonAst ast) {
            Services = services ?? throw new ArgumentNullException(nameof(services));
            Module = module ?? throw new ArgumentNullException(nameof(module));
            Ast = ast ?? throw new ArgumentNullException(nameof(ast));

            GlobalScope = new GlobalScope(module);
            CurrentScope = GlobalScope;
            DefaultLookupOptions = LookupOptions.Normal;

            //Log = services.GetService<ILogger>();
            _diagnostics = services.GetService<IDiagnosticsService>();

            UnknownType = Interpreter.UnknownType ??
                new FallbackBuiltinPythonType(new FallbackBuiltinsModule(Ast.LanguageVersion), BuiltinTypeId.Unknown);
        }

        public PythonAst Ast { get; }
        public IPythonModule Module { get; }
        public LookupOptions DefaultLookupOptions { get; set; }
        public GlobalScope GlobalScope { get; }
        public Scope CurrentScope { get; private set; }
        public IPythonInterpreter Interpreter => Module.Interpreter;
        public bool SuppressBuiltinLookup => Module.ModuleType == ModuleType.Builtins;
        public ILogger Log { get; }
        public IServiceContainer Services { get; }
        public ModuleSymbolTable SymbolTable { get; } = new ModuleSymbolTable();
        public IPythonType UnknownType { get; }

        public LocationInfo GetLoc(Node node) => node.GetLocation(Module, Ast);
        public LocationInfo GetLocOfName(Node node, NameExpression header) => node.GetLocationOfName(header, Module, Ast);

        public Task<IMember> GetValueFromExpressionAsync(Expression expr, CancellationToken cancellationToken = default)
            => GetValueFromExpressionAsync(expr, DefaultLookupOptions, cancellationToken);

        public async Task<IMember> GetValueFromExpressionAsync(Expression expr, LookupOptions options, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            if (expr == null) {
                return null;
            }

            while (expr is ParenthesisExpression parExpr) {
                expr = parExpr.Expression;
            }

            IMember m;
            switch (expr) {
                case NameExpression nex:
                    m = GetValueFromName(nex, options);
                    break;
                case MemberExpression mex:
                    m = await GetValueFromMemberAsync(mex, cancellationToken);
                    break;
                case CallExpression cex:
                    m = await GetValueFromCallableAsync(cex, cancellationToken);
                    break;
                case UnaryExpression uex:
                    m = await GetValueFromUnaryOpAsync(uex, cancellationToken);
                    break;
                case IndexExpression iex:
                    m = await GetValueFromIndexAsync(iex, cancellationToken);
                    break;
                case ConditionalExpression coex:
                    m = await GetValueFromConditionalAsync(coex, cancellationToken);
                    break;
                case ListExpression listex:
                    m = await GetValueFromListAsync(listex, cancellationToken);
                    break;
                case DictionaryExpression dictex:
                    m = await GetValueFromDictionaryAsync(dictex, cancellationToken);
                    break;
                case SetExpression setex:
                    m = await GetValueFromSetAsync(setex, cancellationToken);
                    break;
                case TupleExpression tex:
                    m = await GetValueFromTupleAsync(tex, cancellationToken);
                    break;
                case YieldExpression yex:
                    m = await GetValueFromExpressionAsync(yex.Expression, cancellationToken);
                    break;
                default:
                    m = await GetValueFromBinaryOpAsync(expr, cancellationToken) ?? GetConstantFromLiteral(expr, options);
                    break;
            }
            if (m == null) {
                Log?.Log(TraceEventType.Verbose, $"Unknown expression: {expr.ToCodeString(Ast).Trim()}");
            }
            return m;
        }

        private IMember GetValueFromName(NameExpression expr, LookupOptions options) {
            if (string.IsNullOrEmpty(expr?.Name)) {
                return null;
            }

            var existing = LookupNameInScopes(expr.Name, options);
            if (existing != null) {
                return existing;
            }

            if (expr.Name == Module.Name) {
                return Module;
            }
            Log?.Log(TraceEventType.Verbose, $"Unknown name: {expr.Name}");
            return UnknownType;
        }

        private async Task<IMember> GetValueFromMemberAsync(MemberExpression expr, CancellationToken cancellationToken = default) {
            if (expr?.Target == null || string.IsNullOrEmpty(expr.Name)) {
                return null;
            }

            IPythonInstance instance = null;
            var m = await GetValueFromExpressionAsync(expr.Target, cancellationToken);
            if (m is IPythonType typeInfo) {
                var member = typeInfo.GetMember(expr.Name);
                // If container is class/type info rather than the instance, then the method is an unbound function.
                // Example: C.f where f is a method of C. Compare to C().f where f is bound to the instance of C.
                if (member is PythonFunctionType f && !f.IsStatic && !f.IsClassMethod) {
                    return f.ToUnbound();
                }
                instance = new PythonInstance(typeInfo);
            }

            instance = instance ?? m as IPythonInstance;
            var type = m.GetPythonType(); // Try inner type
            var value = type?.GetMember(expr.Name);
            switch (value) {
                case IPythonClassType _:
                    return value;
                case IPythonPropertyType prop:
                    return prop.Call(instance, prop.Name, ArgumentSet.Empty);
                case IPythonType p:
                    return new PythonBoundType(p, instance, GetLoc(expr));
                case null:
                    Log?.Log(TraceEventType.Verbose, $"Unknown member {expr.ToCodeString(Ast).Trim()}");
                    return UnknownType;
                default:
                    return value;
            }
        }

        private async Task<IMember> GetValueFromConditionalAsync(ConditionalExpression expr, CancellationToken cancellationToken = default) {
            if (expr == null) {
                return null;
            }

            var trueValue = await GetValueFromExpressionAsync(expr.TrueExpression, cancellationToken);
            var falseValue = await GetValueFromExpressionAsync(expr.FalseExpression, cancellationToken);

            return trueValue ?? falseValue;
        }

        private void AddDiagnostics(IEnumerable<DiagnosticsEntry> entries) {
            foreach (var e in entries) {
                _diagnostics?.Add(e);
            }
        }
    }
}
