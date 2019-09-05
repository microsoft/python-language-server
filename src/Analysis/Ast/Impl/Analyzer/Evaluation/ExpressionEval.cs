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
using Microsoft.Python.Analysis.Analyzer.Symbols;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Disposables;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Evaluation {
    /// <summary>
    /// Helper class that provides methods for looking up variables
    /// and types in a chain of scopes during analysis.
    /// </summary>
    internal sealed partial class ExpressionEval : IExpressionEvaluator {
        private readonly Stack<Scope> _openScopes = new Stack<Scope>();
        private readonly object _lock = new object();
        private readonly List<DiagnosticsEntry> _diagnostics = new List<DiagnosticsEntry>();

        public ExpressionEval(IServiceContainer services, IPythonModule module, PythonAst ast) {
            Services = services ?? throw new ArgumentNullException(nameof(services));
            Module = module ?? throw new ArgumentNullException(nameof(module));
            Ast = ast ?? throw new ArgumentNullException(nameof(ast));

            GlobalScope = new GlobalScope(module, ast);
            CurrentScope = GlobalScope;
            DefaultLocation = new Location(module);
            //Log = services.GetService<ILogger>();
        }

        public GlobalScope GlobalScope { get; }
        public Scope CurrentScope { get; private set; }
        public bool SuppressBuiltinLookup => Module.ModuleType == ModuleType.Builtins;
        public ILogger Log { get; }
        public ModuleSymbolTable SymbolTable { get; } = new ModuleSymbolTable();
        public IPythonType UnknownType => Interpreter.UnknownType;
        public Location DefaultLocation { get; }
        public IPythonModule BuiltinsModule => Interpreter.ModuleResolution.BuiltinsModule;

        public LocationInfo GetLocationInfo(Node node) => node?.GetLocation(this) ?? LocationInfo.Empty;

        public Location GetLocationOfName(Node node) {
            if (node == null || (Module.ModuleType != ModuleType.User && Module.ModuleType != ModuleType.Library)) {
                return DefaultLocation;
            }

            IndexSpan indexSpan;
            switch (node) {
                case MemberExpression mex:
                    indexSpan = mex.GetNameSpan(Ast).ToIndexSpan(Ast);
                    break;
                case ClassDefinition cd:
                    indexSpan = cd.NameExpression.IndexSpan;
                    break;
                case FunctionDefinition fd:
                    indexSpan = fd.NameExpression.IndexSpan;
                    break;
                case NameExpression nex:
                    indexSpan = nex.IndexSpan;
                    break;
                default:
                    indexSpan = node.IndexSpan;
                    break;
            }

            // Sanity check that AST matches. If it is not, indexSpan typically
            // turns into span at line 1 and very large column. This DOES can
            // produce false positives occasionally.
#if DEBUG
            var sourceSpan = indexSpan.ToSourceSpan(Ast);
            Debug.Assert(sourceSpan.Start.Line > 1 || sourceSpan.Start.Column < 1000);
#endif
            return new Location(Module, indexSpan);
        }

        #region IExpressionEvaluator
        public PythonAst Ast { get; }
        public IPythonModule Module { get; }
        public IPythonInterpreter Interpreter => Module.Interpreter;
        public IServiceContainer Services { get; }
        IScope IExpressionEvaluator.CurrentScope => CurrentScope;
        IGlobalScope IExpressionEvaluator.GlobalScope => GlobalScope;
        public LocationInfo GetLocation(Node node) => node?.GetLocation(this) ?? LocationInfo.Empty;
        public IEnumerable<DiagnosticsEntry> Diagnostics => _diagnostics;

        public void ReportDiagnostics(Uri documentUri, DiagnosticsEntry entry) {
            // Do not add if module is library, etc. Only handle user code.
            if (entry.ShouldReport(Module)) {
                lock (_lock) {
                    _diagnostics.Add(entry);
                }
            }
        }

        public IDisposable OpenScope(IScope scope) {
            if (!(scope is Scope s)) {
                return Disposable.Empty;
            }
            _openScopes.Push(s);
            CurrentScope = s;
            return new ScopeTracker(this);
        }

        public IDisposable OpenScope(IPythonModule module, ScopeStatement scope) => OpenScope(module, scope, out _);
        #endregion

        public IMember GetValueFromExpression(Expression expr, LookupOptions options = LookupOptions.Normal) {
            if (expr == null) {
                return null;
            }

            expr = expr.RemoveParenthesis();

            IMember m;
            switch (expr) {
                case NameExpression nex:
                    m = GetValueFromName(nex, options);
                    break;
                case MemberExpression mex:
                    m = GetValueFromMember(mex);
                    break;
                case CallExpression cex:
                    m = GetValueFromCallable(cex);
                    break;
                case UnaryExpression uex:
                    m = GetValueFromUnaryOp(uex);
                    break;
                case IndexExpression iex:
                    m = GetValueFromIndex(iex);
                    break;
                case ConditionalExpression coex:
                    m = GetValueFromConditional(coex);
                    break;
                case ListExpression listex:
                    m = GetValueFromList(listex);
                    break;
                case DictionaryExpression dictex:
                    m = GetValueFromDictionary(dictex);
                    break;
                case SetExpression setex:
                    m = GetValueFromSet(setex);
                    break;
                case TupleExpression tex:
                    m = GetValueFromTuple(tex);
                    break;
                case YieldExpression yex:
                    m = GetValueFromExpression(yex.Expression);
                    break;
                case GeneratorExpression genex:
                    m = GetValueFromGenerator(genex);
                    break;
                case Comprehension comp:
                    m = GetValueFromComprehension(comp);
                    break;
                case LambdaExpression lambda:
                    m = GetValueFromLambda(lambda);
                    break;
                case FString fString:
                    m = GetValueFromFString(fString);
                    break;
                case FormatSpecifier formatSpecifier:
                    m = GetValueFromFormatSpecifier(formatSpecifier);
                    break;
                case NamedExpression namedExpr:
                    m = GetValueFromExpression(namedExpr.Value);
                    break;
                // indexing with nothing, e.g Generic[]
                case ErrorExpression error:
                    m = null;
                    break;
                default:
                    m = GetValueFromBinaryOp(expr) ?? GetConstantFromLiteral(expr);
                    break;
            }
            if (m == null) {
                Log?.Log(TraceEventType.Verbose, $"Unknown expression: {expr.ToCodeString(Ast).Trim()}");
            }
            return m;
        }

        internal void ClearCache() => _scopeLookupCache.Clear();

        private IMember GetValueFromFormatSpecifier(FormatSpecifier formatSpecifier)
            => new PythonFString(formatSpecifier.Unparsed, Interpreter);

        private IMember GetValueFromFString(FString fString)
            => new PythonFString(fString.Unparsed, Interpreter);

        private IMember GetValueFromName(NameExpression expr, LookupOptions options = LookupOptions.Normal) {
            if (expr == null || string.IsNullOrEmpty(expr.Name)) {
                return null;
            }

            var member = LookupNameInScopes(expr.Name, out _, out var v, options);
            if (member != null) {
                v?.AddReference(GetLocationOfName(expr));
                switch (member.GetPythonType()) {
                    case IPythonClassType cls:
                        SymbolTable.Evaluate(cls.ClassDefinition);
                        break;
                    case IPythonFunctionType fn:
                        SymbolTable.Evaluate(fn.FunctionDefinition);
                        break;
                    case IPythonPropertyType prop:
                        SymbolTable.Evaluate(prop.FunctionDefinition);
                        break;
                }
                return member;
            }

            Log?.Log(TraceEventType.Verbose, $"Unknown name: {expr.Name}");
            return UnknownType;
        }

        private IMember GetValueFromMember(MemberExpression expr) {
            if (expr?.Target == null || string.IsNullOrEmpty(expr.Name)) {
                return null;
            }

            var m = GetValueFromExpression(expr.Target);
            var type = m.GetPythonType();
            var value = type?.GetMember(expr.Name);
            type?.AddMemberReference(expr.Name, this, GetLocationOfName(expr));

            if (type is IPythonModule) {
                return value;
            }

            IPythonInstance instance = null;
            if (m == type) {
                // If container is class/type info rather than the instance, then the method is an unbound function.
                // Example: C.f where f is a method of C. Compare to C().f where f is bound to the instance of C.
                if (value is PythonFunctionType f && f.DeclaringType != null && !f.IsStatic && !f.IsClassMethod) {
                    f.AddReference(GetLocationOfName(expr));
                    return f.ToUnbound();
                }
                instance = typeInfo.CreateInstance(ArgumentSet.Empty(expr, this));
            }

            instance = instance ?? m as IPythonInstance;

            // Class type GetMember returns a type. However, class members are
            // mostly instances (consider self.x = 1, x is an instance of int).
            // However, it is indeed possible to have them as types, like in
            //  class X ...
            //  class C: ...
            //      self.x = X
            // which is somewhat rare as compared to self.x = X() but does happen.

            switch (value) {
                case IPythonClassType _:
                    return value;
                case IPythonPropertyType prop:
                    return prop.Call(instance, prop.Name, ArgumentSet.Empty(expr, this));
                case IPythonType p:
                    return new PythonBoundType(p, instance);
                case null:
                    Log?.Log(TraceEventType.Verbose, $"Unknown member {expr.ToCodeString(Ast).Trim()}");
                    return UnknownType;
                default:
                    return value;
            }
        }

        private IMember GetValueFromConditional(ConditionalExpression expr) {
            if (expr == null) {
                return null;
            }

            var trueValue = GetValueFromExpression(expr.TrueExpression);
            var falseValue = GetValueFromExpression(expr.FalseExpression);

            return trueValue ?? falseValue ?? UnknownType;
        }
    }
}
