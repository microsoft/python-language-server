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
using Microsoft.Python.Analysis.Extensions;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
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

        [DebuggerStepThrough]
        public Task<IMember> GetValueFromExpressionAsync(Expression expr, CancellationToken cancellationToken = default)
            => GetValueFromExpressionAsync(expr, DefaultLookupOptions, cancellationToken);

        public async Task<IMember> GetValueFromExpressionAsync(Expression expr, LookupOptions options, CancellationToken cancellationToken = default) {
            if (expr is ParenthesisExpression parExpr) {
                expr = parExpr.Expression;
            }
            if (expr == null) {
                return null;
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
            _log?.Log(TraceEventType.Verbose, $"Unknown name: {expr.Name}");
            return UnknownType;
        }

        private async Task<IMember> GetValueFromMemberAsync(MemberExpression expr, CancellationToken cancellationToken = default) {
            if (expr?.Target == null || string.IsNullOrEmpty(expr.Name)) {
                return null;
            }

            var m = await GetValueFromExpressionAsync(expr.Target, cancellationToken);
            var typeInfo = m as IPythonType; // See if value is type info.
            var value = typeInfo?.GetMember(expr.Name);
            // If container is class (type) info rather than the instance, then method is an unbound function.
            value = typeInfo != null && value is PythonFunction f && !f.IsStatic ? f.ToUnbound() : value;

            var type = m.GetPythonType(); // Try inner type
            value = value ?? type?.GetMember(expr.Name);
            switch (value) {
                case IPythonProperty p:
                    return await GetPropertyReturnTypeAsync(p, expr, cancellationToken);
                case null:
                    _log?.Log(TraceEventType.Verbose, $"Unknown member {expr.ToCodeString(Ast).Trim()}");
                    return UnknownType;
                default:
                    return value;
            }
        }

        private Task<IMember> GetValueFromUnaryOpAsync(UnaryExpression expr, CancellationToken cancellationToken = default)
            => GetValueFromExpressionAsync(expr?.Expression, cancellationToken);

        private async Task<IMember> GetValueFromBinaryOpAsync(Expression expr, CancellationToken cancellationToken = default) {
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

        private async Task<IMember> GetValueFromIndexAsync(IndexExpression expr, CancellationToken cancellationToken = default) {
            if (expr?.Target == null) {
                return null;
            }

            if (expr.Index is SliceExpression || expr.Index is TupleExpression) {
                // When slicing, assume result is the same type
                return await GetValueFromExpressionAsync(expr.Target, cancellationToken);
            }

            var target = await GetValueFromExpressionAsync(expr.Target, cancellationToken);
            // TODO: handle typing module
            return target;
        }

        private async Task<IMember> GetValueFromConditionalAsync(ConditionalExpression expr, CancellationToken cancellationToken = default) {
            if (expr == null) {
                return null;
            }

            var trueValue = await GetValueFromExpressionAsync(expr.TrueExpression, cancellationToken);
            var falseValue = await GetValueFromExpressionAsync(expr.FalseExpression, cancellationToken);

            return trueValue ?? falseValue;
        }

        private async Task<IMember> GetValueFromCallableAsync(CallExpression expr, LookupOptions options, CancellationToken cancellationToken = default) {
            if (expr?.Target == null) {
                return null;
            }

            var target = await GetValueFromExpressionAsync(expr.Target, cancellationToken);
            IMember value = null;
            switch (target) {
                case IPythonInstance pi:
                    // Call on an instance such as 'a = 1; a()'
                    // If instance is a function (such as an unbound method), then invoke it.
                    if (pi.GetPythonType() is IPythonFunction pif) {
                        return await GetValueFromFunctionAsync(pif, expr, cancellationToken);
                        // TODO: handle __call__ ?
                    }
                    break;
                case IPythonFunction pf:
                    value = await GetValueFromFunctionAsync(pf, expr, cancellationToken);
                    break;
                case IPythonType t:
                    // Target is type (info), the call creates instance.
                    // For example, 'x = C; y = x()' or 'x = C()' where C is class
                    value = new PythonInstance(t, GetLoc(expr));
                    break;
            }

            if (value == null) {
                _log?.Log(TraceEventType.Verbose, $"Unknown callable: {expr.Target.ToCodeString(Ast).Trim()}");
            }
            return value;
        }

        private async Task<IMember> GetValueFromFunctionAsync(IPythonFunction fn, Expression expr, CancellationToken cancellationToken = default) {
            if (!(expr is CallExpression callExpr)) {
                Debug.Assert(false, "Call to GetValueFromFunctionAsync with non-call expression.");
                return null;
            }

            // Determine argument types
            var args = new List<IMember>();
            // For static and regular methods add 'self' or 'cls'
            if (fn.DeclaringType != null && !(fn.IsUnbound() || fn.IsClassMethod)) {
                // TODO: tell between static and regular by passing instance and not class type info.
                args.Add(fn.DeclaringType);
            }

            foreach (var a in callExpr.Args.MaybeEnumerate()) {
                var type = await GetValueFromExpressionAsync(a.Expression, cancellationToken);
                args.Add(type ?? UnknownType);
            }

            // Find best overload match
            // TODO: match better, see ArgumentSet class in DDG.
            var overload = fn.Overloads.FirstOrDefault(o => o.Parameters.Count == args.Count);
            overload = overload ?? fn.Overloads
                       .Where(o => o.Parameters.Count >= args.Count)
                       .FirstOrDefault(o => {
                           // Match so overall param count is bigger, but required params
                           // count is less or equal to the passed arguments.
                           var requiredParams = o.Parameters.Where(p => string.IsNullOrEmpty(p.DefaultValue)).ToArray();
                           return requiredParams.Length <= args.Count;
                       });

            // TODO: provide instance
            var returnType = GetFunctionReturnType(overload, null, args);
            if (returnType.IsUnknown()) {
                // Function may not have been walked yet. Do it now.
                await _functionWalkers.ProcessFunctionAsync(fn.FunctionDefinition, cancellationToken);
                returnType = GetFunctionReturnType(overload, null, args);
            }
            return returnType ?? UnknownType;
        }

        private IPythonType GetFunctionReturnType(IPythonFunctionOverload o, IPythonInstance instance, IReadOnlyList<IMember> args)
            => o?.GetReturnType(instance, args) ?? UnknownType;

        private async Task<IPythonType> GetPropertyReturnTypeAsync(IPythonProperty p, Expression expr, CancellationToken cancellationToken = default) {
            if (p.Type.IsUnknown()) {
                // Function may not have been walked yet. Do it now.
                await _functionWalkers.ProcessFunctionAsync(p.FunctionDefinition, cancellationToken);
            }
            return p.Type ?? UnknownType;
        }

        public IPythonInstance GetConstantFromLiteral(Expression expr, LookupOptions options) {
            var location = GetLoc(expr);
            if (expr is ConstantExpression ce) {
                switch (ce.Value) {
                    case string s:
                        return new PythonStringLiteral(s, Interpreter.GetBuiltinType(BuiltinTypeId.Unicode), location);
                    case AsciiString b:
                        return new PythonStringLiteral(b.String, Interpreter.GetBuiltinType(BuiltinTypeId.Bytes), location);
                }
            }
            return new PythonInstance(GetTypeFromLiteral(expr) ?? UnknownType, location);
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
                case PythonMemberType.Module:
                    return Interpreter.GetBuiltinType(BuiltinTypeId.Module);
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
                        IPythonType value = null;
                        if (x is NameExpression ne) {
                            value = GetInScope(ne.Name)?.GetPythonType();
                        }
                        return value ?? UnknownType;
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

        public IMember GetInScope(string name)
            => CurrentScope.Variables.TryGetVariable(name, out var variable) ? variable.Value : null;

        public T GetInScope<T>(string name) where T : class, IMember
            => CurrentScope.Variables.TryGetVariable(name, out var variable) ? variable.Value as T : null;

        public void DeclareVariable(string name, IMember value, Node expression)
            => DeclareVariable(name, value, GetLoc(expression));

        public void DeclareVariable(string name, IMember value, LocationInfo location) {
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
                if (Module != Interpreter.ModuleResolution.BuiltinModule && options.HasFlag(LookupOptions.Builtins)) {
                    value = Interpreter.ModuleResolution.BuiltinModule.GetMember(name);
                }
            }

            return value;
        }

        public IPythonType GetTypeFromAnnotation(Expression expr) {
            if (expr == null) {
                return null;
            }
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
                var s = _lookup.CurrentScope;
                _lookup.CurrentScope = _lookup._openScopes.Pop();
            }
        }
    }
}
