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
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Evaluation {
    /// <summary>
    /// Helper class that provides methods for looking up variables
    /// and types in a chain of scopes during analysis.
    /// </summary>
    internal sealed partial class ExpressionEval: IExpressionEvaluator {
        private readonly Stack<Scope> _openScopes = new Stack<Scope>();
        private readonly List<DiagnosticsEntry> _diagnostics = new List<DiagnosticsEntry>();
        private readonly object _lock = new object();

        public ExpressionEval(IServiceContainer services, IPythonModule module, PythonAst ast) {
            Services = services ?? throw new ArgumentNullException(nameof(services));
            Module = module ?? throw new ArgumentNullException(nameof(module));
            Ast = ast ?? throw new ArgumentNullException(nameof(ast));

            GlobalScope = new GlobalScope(module);
            CurrentScope = GlobalScope;
            DefaultLookupOptions = LookupOptions.Normal;

            //Log = services.GetService<ILogger>();
        }

        public LookupOptions DefaultLookupOptions { get; set; }
        public GlobalScope GlobalScope { get; }
        public Scope CurrentScope { get; private set; }
        public bool SuppressBuiltinLookup => Module.ModuleType == ModuleType.Builtins;
        public ILogger Log { get; }
        public ModuleSymbolTable SymbolTable { get; } = new ModuleSymbolTable();
        public IPythonType UnknownType => Interpreter.UnknownType;

        public LocationInfo GetLoc(Node node) => node?.GetLocation(Module, Ast) ?? LocationInfo.Empty;
        public LocationInfo GetLocOfName(Node node, NameExpression header) => node?.GetLocationOfName(header, Module, Ast) ?? LocationInfo.Empty;

        #region IExpressionEvaluator
        public PythonAst Ast { get; }
        public IPythonModule Module { get; }
        public IPythonInterpreter Interpreter => Module.Interpreter;
        public IServiceContainer Services { get; }
        IScope IExpressionEvaluator.CurrentScope => CurrentScope;
        IGlobalScope IExpressionEvaluator.GlobalScope => GlobalScope;
        public LocationInfo GetLocation(Node node) => node?.GetLocation(Module, Ast) ?? LocationInfo.Empty;
        public IEnumerable<DiagnosticsEntry> Diagnostics => _diagnostics;

        public void ReportDiagnostics(Uri documentUri, DiagnosticsEntry entry) {
            // Do not add if module is library, etc. Only handle user code.
            if (Module.ModuleType == ModuleType.User) {
                lock (_lock) {
                    _diagnostics.Add(entry);
                }
            }
        }

        public IMember GetValueFromExpression(Expression expr)
            => GetValueFromExpression(expr, DefaultLookupOptions);

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

        public IMember GetValueFromExpression(Expression expr, LookupOptions options) {
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
                default:
                    m = GetValueFromBinaryOp(expr) ?? GetConstantFromLiteral(expr, options);
                    break;
            }
            if (m == null) {
                Log?.Log(TraceEventType.Verbose, $"Unknown expression: {expr.ToCodeString(Ast).Trim()}");
            }
            return m;
        }

        private IMember GetValueFromFormatSpecifier(FormatSpecifier formatSpecifier) {
            return new PythonFString(formatSpecifier.Unparsed, Interpreter, GetLoc(formatSpecifier));
        }

        private IMember GetValueFromFString(FString fString) {
            return new PythonFString(fString.Unparsed, Interpreter, GetLoc(fString));
        }

        private IMember GetValueFromName(NameExpression expr, LookupOptions options) {
            if (expr == null || string.IsNullOrEmpty(expr.Name)) {
                return null;
            }

            var member = LookupNameInScopes(expr.Name, options);
            if (member != null) {
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

            IPythonInstance instance = null;
            var m = GetValueFromExpression(expr.Target);
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
            var type = m?.GetPythonType(); // Try inner type
            var value = type?.GetMember(expr.Name);

            if (type is IPythonModule) {
                return value;
            }

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
