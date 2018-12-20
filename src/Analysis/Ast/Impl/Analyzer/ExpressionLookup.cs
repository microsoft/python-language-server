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
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    /// <summary>
    /// Helper class that provides methods for looking up variables
    /// and types in a chain of scopes during analysis.
    /// </summary>
    internal sealed class ExpressionLookup {
        private readonly AnalysisFunctionWalkerSet _functionWalkers;
        private readonly Stack<Scope> _openScopes = new Stack<Scope>();
        private readonly ILogger _log;

        internal IPythonType UnknownType { get; }

        public ExpressionLookup(
            IServiceContainer services,
            IPythonModule module,
            PythonAst ast,
            Scope moduleScope,
            AnalysisFunctionWalkerSet functionWalkers
        ) {
            Ast = ast ?? throw new ArgumentNullException(nameof(ast));
            Module = module ?? throw new ArgumentNullException(nameof(module));
            CurrentScope = moduleScope ?? throw new ArgumentNullException(nameof(moduleScope));

            _log = services.GetService<ILogger>();
            _functionWalkers = functionWalkers ?? throw new ArgumentNullException(nameof(functionWalkers));

            DefaultLookupOptions = LookupOptions.Normal;

            UnknownType = Interpreter.GetBuiltinType(BuiltinTypeId.Unknown) ??
                new FallbackBuiltinPythonType(new FallbackBuiltinModule(Ast.LanguageVersion), BuiltinTypeId.Unknown);
        }

        public PythonAst Ast { get; }
        public IPythonModule Module { get; }
        public LookupOptions DefaultLookupOptions { get; set; }
        public Scope CurrentScope { get; private set; }
        public IPythonInterpreter Interpreter => Module.Interpreter;

        public LocationInfo GetLoc(Node node) {
            if (node == null || node.StartIndex >= node.EndIndex) {
                return LocationInfo.Empty;
            }

            var start = node.GetStart(Ast);
            var end = node.GetEnd(Ast);
            return new LocationInfo(Module.FilePath, Module.Uri, start.Line, start.Column, end.Line, end.Column);
        }

        public LocationInfo GetLocOfName(Node node, NameExpression header) {
            var loc = GetLoc(node);
            if (loc == null || header == null) {
                return LocationInfo.Empty;
            }

            var nameStart = header.GetStart(Ast);
            if (!nameStart.IsValid) {
                return loc;
            }

            if (nameStart.Line > loc.StartLine || (nameStart.Line == loc.StartLine && nameStart.Column > loc.StartColumn)) {
                return new LocationInfo(loc.FilePath, loc.DocumentUri, nameStart.Line, nameStart.Column, loc.EndLine, loc.EndColumn);
            }

            return loc;
        }

        public IPythonType GetTypeFromAnnotation(Expression expr) {
            if (expr == null) {
                return null;
            }
            var ann = new TypeAnnotation(Ast.LanguageVersion, expr);
            return ann.GetValue(new TypeAnnotationConverter(this));
        }

        [DebuggerStepThrough]
        public Task<IPythonType> GetValueFromExpressionAsync(Expression expr, CancellationToken cancellationToken = default)
            => GetValueFromExpressionAsync(expr, DefaultLookupOptions, cancellationToken);

        public async Task<IPythonType> GetValueFromExpressionAsync(Expression expr, LookupOptions options, CancellationToken cancellationToken = default) {
            if (expr is ParenthesisExpression parExpr) {
                expr = parExpr.Expression;
            }

            if (expr == null) {
                return null;
            }

            IPythonType m;
            switch (expr) {
                case NameExpression nex:
                    m = GetValueFromName(nex, options);
                    break;
                case MemberExpression mex:
                    m = await GetValueFromMemberAsync(mex, cancellationToken);
                    break;
                case CallExpression cex:
                    m = await GetValueFromCallableAsync(cex, options, cancellationToken);
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
                default:
                    m = await GetValueFromBinaryOpAsync(expr, cancellationToken) ?? GetConstantFromLiteral(expr, options);
                    break;
            }
            if (m == null) {
                _log?.Log(TraceEventType.Verbose, $"Unknown expression: {expr.ToCodeString(Ast).Trim()}");
            }
            return m;
        }

        private IPythonType GetValueFromName(NameExpression expr, LookupOptions options) {
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
            _log?.Log(TraceEventType.Verbose, $"Unknown name: {expr.Name}");
            return UnknownType;
        }

        private async Task<IPythonType> GetValueFromMemberAsync(MemberExpression expr, CancellationToken cancellationToken = default) {
            if (expr?.Target == null || string.IsNullOrEmpty(expr.Name)) {
                return null;
            }

            var mc = await GetValueFromExpressionAsync(expr.Target, cancellationToken);
            var value = mc.GetMember(expr.Name);
            // If container is class rather than the instance, then method is an unbound function.
            value = mc is IPythonClass c && value is PythonFunction f && !f.IsStatic ? f.ToUnbound() : value;

            switch (value) {
                case IPythonProperty p:
                    return await GetPropertyReturnTypeAsync(p, expr, cancellationToken);
                case IPythonFunction fn:
                    return await GetValueFromFunctionAsync(fn, expr, cancellationToken);
                case null:
                    _log?.Log(TraceEventType.Verbose, $"Unknown member {expr.ToCodeString(Ast).Trim()}");
                    return UnknownType;
                default:
                    return value;
            }
        }

        private Task<IPythonType> GetValueFromUnaryOpAsync(UnaryExpression expr, CancellationToken cancellationToken = default)
            => GetValueFromExpressionAsync(expr?.Expression, cancellationToken);

        private async Task<IPythonType> GetValueFromBinaryOpAsync(Expression expr, CancellationToken cancellationToken = default) {
            if (expr is AndExpression || expr is OrExpression) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.Bool);
            }

            if (expr is BinaryExpression binop) {
                if (binop.Left == null) {
                    return null;
                }

                // TODO: Specific parsing
                switch (binop.Operator) {
                    case PythonOperator.Equal:
                    case PythonOperator.GreaterThan:
                    case PythonOperator.GreaterThanOrEqual:
                    case PythonOperator.In:
                    case PythonOperator.Is:
                    case PythonOperator.IsNot:
                    case PythonOperator.LessThan:
                    case PythonOperator.LessThanOrEqual:
                    case PythonOperator.Not:
                    case PythonOperator.NotEqual:
                    case PythonOperator.NotIn:
                        // Assume all of these return True/False
                        return Interpreter.GetBuiltinType(BuiltinTypeId.Bool);
                }

                // Try LHS, then, if unknown, try RHS. Example: y = 1 when y is not declared by the walker yet.
                var value = await GetValueFromExpressionAsync(binop.Left, cancellationToken);
                return value.IsUnknown() ? await GetValueFromExpressionAsync(binop.Right, cancellationToken) : value;
            }

            return null;
        }

        private async Task<IPythonType> GetValueFromIndexAsync(IndexExpression expr, CancellationToken cancellationToken = default) {
            if (expr?.Target == null) {
                return null;
            }

            if (expr.Index is SliceExpression || expr.Index is TupleExpression) {
                // When slicing, assume result is the same type
                return await GetValueFromExpressionAsync(expr.Target, cancellationToken);
            }

            var value = await GetValueFromExpressionAsync(expr.Target, cancellationToken);
            var type = GetTypeFromValue(value);
            if (!type.IsUnknown()) {
                if (AstTypingModule.IsTypingType(type)) {
                    return type;
                }

                switch (type.TypeId) {
                    case BuiltinTypeId.Bytes:
                        return Ast.LanguageVersion.Is3x()
                            ? Interpreter.GetBuiltinType(BuiltinTypeId.Int)
                            : Interpreter.GetBuiltinType(BuiltinTypeId.Bytes);
                    case BuiltinTypeId.Unicode:
                        return Interpreter.GetBuiltinType(BuiltinTypeId.Unicode);
                }

                if (type.MemberType == PythonMemberType.Class) {
                    // When indexing into a type, assume result is the type
                    // TODO: Proper generic handling
                    return type;
                }

                _log?.Log(TraceEventType.Verbose, $"Unknown index: {type.TypeId}, {expr.ToCodeString(Ast, CodeFormattingOptions.Traditional).Trim()}");
            } else {
                _log?.Log(TraceEventType.Verbose, $"Unknown index: ${expr.ToCodeString(Ast, CodeFormattingOptions.Traditional).Trim()}");
            }
            return null;
        }

        private async Task<IPythonType> GetValueFromConditionalAsync(ConditionalExpression expr, CancellationToken cancellationToken = default) {
            if (expr == null) {
                return null;
            }

            var trueValue = await GetValueFromExpressionAsync(expr.TrueExpression, cancellationToken);
            var falseValue = await GetValueFromExpressionAsync(expr.FalseExpression, cancellationToken);

            return trueValue ?? falseValue;
        }

        private async Task<IPythonType> GetValueFromCallableAsync(CallExpression expr, LookupOptions options, CancellationToken cancellationToken = default) {
            if (expr?.Target == null) {
                return null;
            }

            var m = await GetValueFromExpressionAsync(expr.Target, cancellationToken);
            IPythonType value = null;
            switch (m) {
                case IPythonInstance c when c.Type?.MemberType == PythonMemberType.Class:
                    return c.Type; // typically cls()
                case IPythonClass cls:
                    value = new PythonInstance(cls, GetLoc(expr));
                    break;
                case IPythonFunction pf:
                    value = await GetValueFromFunctionAsync(pf, expr, cancellationToken);
                    break;
                case IPythonType type when type == Interpreter.GetBuiltinType(BuiltinTypeId.Type) && expr.Args.Count >= 1:
                    value = await GetValueFromExpressionAsync(expr.Args[0].Expression, options, cancellationToken);
                    value = GetTypeFromValue(value);
                    break;
                case IPythonType type:
                    value = type;
                    break;
            }

            if (value == null) {
                _log?.Log(TraceEventType.Verbose, $"Unknown callable: {expr.Target.ToCodeString(Ast).Trim()}");
            }
            return value;
        }

        private async Task<IPythonType> GetValueFromFunctionAsync(IPythonFunction fn, Expression expr, CancellationToken cancellationToken = default) {
            // Determine argument types
            List<IPythonType> args = null;
            if (expr is CallExpression callExpr && callExpr.Args.Count > 0) {
                args = new List<IPythonType>();
                foreach (var a in callExpr.Args.MaybeEnumerate()) {
                    var type = await GetValueFromExpressionAsync(a.Expression, cancellationToken);
                    args.Add(type ?? UnknownType);
                }
            }
            
            // Find best overload match

            var returnType = GetFunctionReturnType(fn.Overloads.FirstOrDefault(), args);
            if (returnType.IsUnknown()) {
                // Function may not have been walked yet. Do it now.
                await _functionWalkers.ProcessFunctionAsync(fn.FunctionDefinition, cancellationToken);
                returnType = GetFunctionReturnType(fn.Overloads.FirstOrDefault(), args);
            }
            return !returnType.IsUnknown() ? new PythonInstance(returnType, GetLoc(expr)) : null;
        }

        private IPythonType GetFunctionReturnType(IPythonFunctionOverload o, IReadOnlyList<IPythonType> args) => o?.GetReturnType(args) ?? UnknownType;

        private async Task<IPythonType> GetPropertyReturnTypeAsync(IPythonProperty p, Expression expr, CancellationToken cancellationToken = default) {
            if (p.Type.IsUnknown()) {
                // Function may not have been walked yet. Do it now.
                await _functionWalkers.ProcessFunctionAsync(p.FunctionDefinition, cancellationToken);
            }
            return !p.Type.IsUnknown() ? new PythonInstance(p.Type, GetLoc(expr)) : null;
        }

        public IPythonInstance GetConstantFromLiteral(Expression expr, LookupOptions options) {
            if (expr is ConstantExpression ce) {
                if (ce.Value is string s) {
                    return new PythonStringLiteral(s, Interpreter.GetBuiltinType(BuiltinTypeId.Unicode), GetLoc(expr));
                }
                if (ce.Value is AsciiString b) {
                    return new PythonStringLiteral(b.String, Interpreter.GetBuiltinType(BuiltinTypeId.Bytes), GetLoc(expr));
                }
            }

            var type = GetTypeFromLiteral(expr);
            return type != null ? new PythonInstance(type, GetLoc(expr)) : null;
        }

        public IPythonType GetTypeFromValue(IPythonType value) {
            if (value == null) {
                return null;
            }

            var type = (value as IPythonInstance)?.Type;
            if (type != null) {
                return type;
            }

            switch (value.MemberType) {
                case PythonMemberType.Class:
                    return Interpreter.GetBuiltinType(BuiltinTypeId.Type);
                case PythonMemberType.Function:
                    return Interpreter.GetBuiltinType(BuiltinTypeId.Function);
                case PythonMemberType.Method:
                    return Interpreter.GetBuiltinType(BuiltinTypeId.Method);
                case PythonMemberType.Enum:
                case PythonMemberType.EnumInstance:
                    break;
                case PythonMemberType.Module:
                    return Interpreter.GetBuiltinType(BuiltinTypeId.Module);
                case PythonMemberType.Event:
                    break;
            }

            if (value is IPythonFunction f) {
                if (f.IsStatic) {
                    return Interpreter.GetBuiltinType(BuiltinTypeId.StaticMethod);
                }
                if (f.IsClassMethod) {
                    return Interpreter.GetBuiltinType(BuiltinTypeId.ClassMethod);
                }
                return f.DeclaringType == null
                    ? Interpreter.GetBuiltinType(BuiltinTypeId.Function)
                    : Interpreter.GetBuiltinType(BuiltinTypeId.Method);
            }

            if (value is IPythonProperty prop) {
                return prop.Type ?? Interpreter.GetBuiltinType(BuiltinTypeId.Property);
            }
            if (value.MemberType == PythonMemberType.Property) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.Property);
            }

            return Interpreter.GetBuiltinType(BuiltinTypeId.Type);
        }

        public IPythonType GetTypeFromLiteral(Expression expr) {
            if (expr is ConstantExpression ce) {
                if (ce.Value == null) {
                    return Interpreter.GetBuiltinType(BuiltinTypeId.NoneType);
                }
                switch (Type.GetTypeCode(ce.Value.GetType())) {
                    case TypeCode.Boolean: return Interpreter.GetBuiltinType(BuiltinTypeId.Bool);
                    case TypeCode.Double: return Interpreter.GetBuiltinType(BuiltinTypeId.Float);
                    case TypeCode.Int32: return Interpreter.GetBuiltinType(BuiltinTypeId.Int);
                    case TypeCode.String: return Interpreter.GetBuiltinType(BuiltinTypeId.Unicode);
                    case TypeCode.Object:
                        switch (ce.Value) {
                            case Complex _:
                                return Interpreter.GetBuiltinType(BuiltinTypeId.Complex);
                            case AsciiString _:
                                return Interpreter.GetBuiltinType(BuiltinTypeId.Bytes);
                            case BigInteger _:
                                return Interpreter.GetBuiltinType(BuiltinTypeId.Long);
                            case Ellipsis _:
                                return Interpreter.GetBuiltinType(BuiltinTypeId.Ellipsis);
                        }
                        break;
                }
                return null;
            }

            if (expr is ListExpression || expr is ListComprehension) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.List);
            }
            if (expr is DictionaryExpression || expr is DictionaryComprehension) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.Dict);
            }
            if (expr is TupleExpression tex) {
                var types = tex.Items
                    .Select(x => {
                        IPythonType t = null;
                        if (x is NameExpression ne) {
                            t = GetInScope(ne.Name);
                        }
                        return t ?? UnknownType;
                    }).ToArray();
                var res = Interpreter.GetBuiltinType(BuiltinTypeId.Tuple);
                if (types.Length > 0) {
                    var iterRes = Interpreter.GetBuiltinType(BuiltinTypeId.TupleIterator);
                    res = new PythonSequence(res, Module, types, iterRes);
                }
                return res;
            }
            if (expr is SetExpression || expr is SetComprehension) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.Set);
            }
            return expr is LambdaExpression ? Interpreter.GetBuiltinType(BuiltinTypeId.Function) : null;
        }

        public IPythonType GetInScope(string name) => CurrentScope.Variables.GetMember(name);
        public T GetInScope<T>(string name) where T : class, IPythonType => CurrentScope.Variables.GetMember(name) as T;

        public void DeclareVariable(string name, IPythonType type, Node expression)
            => DeclareVariable(name, type, GetLoc(expression));

        public void DeclareVariable(string name, IPythonType type, LocationInfo location) {
            type = type ?? UnknownType;
            var existing = CurrentScope.Variables.GetMember(name);
            if (existing != null) {
                if (existing.IsUnknown() && !type.IsUnknown()) {
                    CurrentScope.DeclareVariable(name, type, location);
                }
            } else {
                CurrentScope.DeclareVariable(name, type, location);
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
        public IPythonType LookupNameInScopes(string name) => LookupNameInScopes(name, DefaultLookupOptions);

        public IPythonType LookupNameInScopes(string name, LookupOptions options) {
            var scopes = CurrentScope.ToChainTowardsGlobal();
            if (scopes.Count == 1) {
                if (!options.HasFlag(LookupOptions.Local) && !options.HasFlag(LookupOptions.Global)) {
                    scopes = null;
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

            if (scopes != null) {
                foreach (var scope in scopes) {
                    var v = scope.Variables[name];
                    if (v != null) {
                        scope.DeclareVariable(name, v.Type, v.Location);
                        return v.Type;
                    }
                }
            }

            if (Module != Interpreter.ModuleResolution.BuiltinModule && options.HasFlag(LookupOptions.Builtins)) {
                return Interpreter.ModuleResolution.BuiltinModule.GetMember(name);
            }
            return null;
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
                var s = _lookup.CurrentScope;
                _lookup.CurrentScope = _lookup._openScopes.Pop();
            }
        }
    }
}
