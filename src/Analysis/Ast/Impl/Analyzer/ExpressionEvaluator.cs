﻿// Copyright(c) Microsoft Corporation
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    /// <summary>
    /// Helper class that provides methods for looking up variables
    /// and types in a chain of scopes during analysis.
    /// </summary>
    internal sealed class ExpressionEvaluator {
        private readonly AstAnalysisFunctionWalkerSet _functionWalkers;
        private readonly Lazy<IPythonModule> _builtinModule;

        private ILogger Log => Module.Interpreter.Log;
        internal IPythonType UnknownType { get; }

        public ExpressionEvaluator(
            IPythonModule module,
            PythonAst ast,
            Scope moduleScope,
            AstAnalysisFunctionWalkerSet functionWalkers,
            IPythonModule builtinModule = null
        ) {
            Ast = ast ?? throw new ArgumentNullException(nameof(ast));
            Module = module;

            _functionWalkers = functionWalkers;
            DefaultLookupOptions = LookupOptions.Normal;

            UnknownType = Interpreter.GetBuiltinType(BuiltinTypeId.Unknown) ??
                new FallbackBuiltinPythonType(new FallbackBuiltinModule(Ast.LanguageVersion), BuiltinTypeId.Unknown);

            _builtinModule = builtinModule == null ? new Lazy<IPythonModule>(ImportBuiltinModule) : new Lazy<IPythonModule>(() => builtinModule);
            CurrentScope = moduleScope;
        }

        public PythonAst Ast { get; }
        public IPythonModule Module { get; }
        public LookupOptions DefaultLookupOptions { get; set; }
        public bool SuppressBuiltinLookup { get; set; }
        public Scope CurrentScope { get; private set; }
        public IPythonInterpreter Interpreter => Module.Interpreter;

        private IPythonModule ImportBuiltinModule() {
            var modName = BuiltinTypeId.Unknown.GetModuleName(Ast.LanguageVersion);
            var mod = Interpreter.ModuleResolution.ImportModule(modName);
            Debug.Assert(mod != null, $"Failed to import {modName}");
            mod?.NotifyImported();
            return mod;
        }

        public LocationInfo GetLoc(Node node) {
            if (node == null || node.StartIndex >= node.EndIndex) {
                return null;
            }

            var start = node.GetStart(Ast);
            var end = node.GetEnd(Ast);
            return new LocationInfo(Module.FilePath, Module.Uri, start.Line, start.Column, end.Line, end.Column);
        }

        public LocationInfo GetLocOfName(Node node, NameExpression header) {
            var loc = GetLoc(node);
            if (loc == null || header == null) {
                return null;
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

        public IEnumerable<IPythonType> GetTypesFromAnnotation(Expression expr) {
            if (expr == null) {
                return Enumerable.Empty<IPythonType>();
            }

            var ann = new TypeAnnotation(Ast.LanguageVersion, expr);
            var m = ann.GetValue(new AstTypeAnnotationConverter(this));
            if (m is IPythonMultipleMembers mm) {
                return mm.GetMembers().OfType<IPythonType>();
            }
            if (m is IPythonType type) {
                return Enumerable.Repeat(type, 1);
            }

            return Enumerable.Empty<IPythonType>();
        }

        public IMember GetValueFromExpression(Expression expr) => GetValueFromExpression(expr, DefaultLookupOptions);

        public IMember GetValueFromExpression(Expression expr, LookupOptions options) {
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
                    m = GetValueFromMember(mex, options);
                    break;
                case CallExpression cex:
                    m = GetValueFromCallable(cex, options);
                    break;
                case UnaryExpression uex:
                    m = GetValueFromUnaryOp(uex, options);
                    break;
                case IndexExpression iex:
                    m = GetValueFromIndex(iex, options);
                    break;
                case ConditionalExpression coex:
                    m = GetValueFromConditional(coex, options);
                    break;
                default:
                    m = GetValueFromBinaryOp(expr, options) ?? GetConstantFromLiteral(expr, options);
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
            return new AstPythonConstant(UnknownType, GetLoc(expr));
        }

        private IMember GetValueFromMember(MemberExpression expr, LookupOptions options) {
            if (expr?.Target == null || string.IsNullOrEmpty(expr?.Name)) {
                return null;
            }

            var e = GetValueFromExpression(expr.Target);
            IMember value = null;
            switch (e) {
                case IMemberContainer mc:
                    value = mc.GetMember(expr.Name);
                    // If container is class rather than the instance, then method is an unbound function.
                    value = mc is IPythonClass c && value is AstPythonFunction f && !f.IsStatic ? f.ToUnbound() : value;
                    break;
                case IPythonMultipleMembers mm:
                    value = mm.GetMembers().OfType<IMemberContainer>()
                        .Select(x => x.GetMember(expr.Name))
                        .ExcludeDefault()
                        .FirstOrDefault();
                    break;
                default:
                    value = GetValueFromPropertyOrFunction(e, expr);
                    break;
            }

            if (value is IPythonProperty p) {
                value = GetPropertyReturnType(p, expr);
            } else if (value == null) {
                Log?.Log(TraceEventType.Verbose, $"Unknown member {expr.ToCodeString(Ast).Trim()}");
                return new AstPythonConstant(UnknownType, GetLoc(expr));
            }
            return value;
        }

        private IMember GetValueFromUnaryOp(UnaryExpression expr, LookupOptions options) {
            if (expr?.Expression == null) {
                return null;
            }
            // Assume that the type after the op is the same as before
            return GetValueFromExpression(expr.Expression);
        }

        private IMember GetValueFromBinaryOp(Expression expr, LookupOptions options) {
            if (expr is AndExpression || expr is OrExpression) {
                return new AstPythonConstant(Interpreter.GetBuiltinType(BuiltinTypeId.Bool), GetLoc(expr));
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
                        return new AstPythonConstant(Interpreter.GetBuiltinType(BuiltinTypeId.Bool), GetLoc(expr));
                }

                // Try LHS, then, if unknown, try RHS. Example: y = 1 when y is not declared by the walker yet.
                var value = GetValueFromExpression(binop.Left);
                return value.IsUnknown() ? GetValueFromExpression(binop.Right) : value;
            }

            return null;
        }

        private IMember GetValueFromIndex(IndexExpression expr, LookupOptions options) {
            if (expr?.Target == null) {
                return null;
            }

            if (expr.Index is SliceExpression || expr.Index is TupleExpression) {
                // When slicing, assume result is the same type
                return GetValueFromExpression(expr.Target);
            }

            var type = GetTypeFromValue(GetValueFromExpression(expr.Target));
            if (!type.IsUnknown()) {
                if (AstTypingModule.IsTypingType(type)) {
                    return type;
                }

                switch (type.TypeId) {
                    case BuiltinTypeId.Bytes:
                        return Ast.LanguageVersion.Is3x() 
                            ? new AstPythonConstant(Interpreter.GetBuiltinType(BuiltinTypeId.Int), GetLoc(expr)) 
                            : new AstPythonConstant(Interpreter.GetBuiltinType(BuiltinTypeId.Bytes), GetLoc(expr));
                    case BuiltinTypeId.Unicode:
                        return new AstPythonConstant(Interpreter.GetBuiltinType(BuiltinTypeId.Unicode), GetLoc(expr));
                }

                if (type.MemberType == PythonMemberType.Class) {
                    // When indexing into a type, assume result is the type
                    // TODO: Proper generic handling
                    return type;
                }

                Log?.Log(TraceEventType.Verbose, $"Unknown index: {type.TypeId}, {expr.ToCodeString(Ast, CodeFormattingOptions.Traditional).Trim()}");
            } else {
                Log?.Log(TraceEventType.Verbose, $"Unknown index: ${expr.ToCodeString(Ast, CodeFormattingOptions.Traditional).Trim()}");
            }
            return null;
        }

        private IMember GetValueFromConditional(ConditionalExpression expr, LookupOptions options) {
            if (expr == null) {
                return null;
            }

            var trueValue = GetValueFromExpression(expr.TrueExpression);
            var falseValue = GetValueFromExpression(expr.FalseExpression);

            return trueValue != null || falseValue != null
                ? AstPythonMultipleMembers.Combine(trueValue, falseValue)
                : null;
        }

        private IMember GetValueFromCallable(CallExpression expr, LookupOptions options) {
            if (expr?.Target == null) {
                return null;
            }

            var m = GetValueFromExpression(expr.Target);
            IMember value = null;
            switch (m) {
                case IPythonFunction pf:
                    value = GetValueFromPropertyOrFunction(pf, expr);
                    break;
                case IPythonType type when type == Interpreter.GetBuiltinType(BuiltinTypeId.Type) && expr.Args.Count >= 1:
                    value = GetTypeFromValue(GetValueFromExpression(expr.Args[0].Expression, options));
                    break;
                case IPythonType type:
                    value = new AstPythonConstant(type, GetLoc(expr));
                    break;
            }

            if (value == null) {
                Log?.Log(TraceEventType.Verbose, "Unknown callable: {expr.Target.ToCodeString(Ast).Trim()}");
            }
            return value;
        }

        private IMember GetValueFromPropertyOrFunction(IMember fn, Expression expr) {
            switch (fn) {
                case IPythonProperty p:
                    return GetPropertyReturnType(p, expr);
                case IPythonFunction f:
                    return GetValueFromFunction(f, expr);
                case IPythonConstant c when c.Type?.MemberType == PythonMemberType.Class:
                    return c.Type; // typically cls()
            }
            return null;
        }

        private IMember GetValueFromFunction(IPythonFunction fn, Expression expr) {
            var returnType = GetFunctionReturnType(fn.Overloads.FirstOrDefault());
            if (returnType.IsUnknown()) {
                // Function may not have been walked yet. Do it now.
                _functionWalkers.ProcessFunction(fn.FunctionDefinition);
                returnType = GetFunctionReturnType(fn.Overloads.FirstOrDefault());
            }
            return !returnType.IsUnknown() ? new AstPythonConstant(returnType, GetLoc(expr)) : null;
        }

        private IPythonType GetFunctionReturnType(IPythonFunctionOverload o)
            => o != null && o.ReturnType.Count > 0 ? o.ReturnType[0] : UnknownType;

        private IMember GetPropertyReturnType(IPythonProperty p, Expression expr) {
            if (p.Type.IsUnknown()) {
                // Function may not have been walked yet. Do it now.
                _functionWalkers.ProcessFunction(p.FunctionDefinition);
            }
            return !p.Type.IsUnknown() ? new AstPythonConstant(p.Type, GetLoc(expr)) : null;
        }

        public IPythonConstant GetConstantFromLiteral(Expression expr, LookupOptions options) {
            if (expr is ConstantExpression ce) {
                if (ce.Value is string s) {
                    return new AstPythonStringLiteral(s, Interpreter.GetBuiltinType(BuiltinTypeId.Unicode), GetLoc(expr));
                }
                if (ce.Value is AsciiString b) {
                    return new AstPythonStringLiteral(b.String, Interpreter.GetBuiltinType(BuiltinTypeId.Bytes), GetLoc(expr));
                }
            }

            var type = GetTypeFromLiteral(expr);
            return type != null ? new AstPythonConstant(type, GetLoc(expr)) : null;
        }

        public IEnumerable<IPythonType> GetTypesFromValue(IMember value) {
            if (value is IPythonMultipleMembers mm) {
                return mm.GetMembers().Select(GetTypeFromValue).Distinct();
            }
            var t = GetTypeFromValue(value);
            return t != null ? Enumerable.Repeat(t, 1) : Enumerable.Empty<IPythonType>();
        }

        public IPythonType GetTypeFromValue(IMember value) {
            if (value == null) {
                return null;
            }

            var type = (value as IPythonConstant)?.Type;
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

            if (value is IPythonMultipleMembers mm) {
                return AstPythonMultipleMembers.CreateAs<IPythonType>(mm.GetMembers());
            }

            if (value is IPythonType) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.Type);
            }

#if DEBUG
            var implements = string.Join(", ", new[] { value.GetType().FullName }.Concat(value.GetType().GetInterfaces().Select(i => i.Name)));
            Debug.Fail("Unhandled type() value: " + implements);
#endif
            return null;
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
                            t = (GetInScope(ne.Name) as AstPythonConstant)?.Type;
                        }
                        return t ?? Interpreter.GetBuiltinType(BuiltinTypeId.Unknown);
                    }).ToArray();
                var res = Interpreter.GetBuiltinType(BuiltinTypeId.Tuple);
                if (types.Length > 0) {
                    var iterRes = Interpreter.GetBuiltinType(BuiltinTypeId.TupleIterator);
                    res = new AstPythonSequence(res, Module, types, iterRes);
                }
                return res;
            }
            if (expr is SetExpression || expr is SetComprehension) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.Set);
            }
            if (expr is LambdaExpression) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.Function);
            }

            return null;
        }

        public IMember GetInScope(string name)
            => CurrentScope.Variables.TryGetValue(name, out var m) ? m : null;

        public void DeclareVariable(string name, IMember value, bool mergeWithExisting = true, ConcurrentDictionary<string, IMember> scope = null) {
            if (value == null) {
                return;
            }
            if (mergeWithExisting && CurrentScope.Variables.TryGetValue(name, out var existing) && existing != null) {
                if (existing.IsUnknown()) {
                    CurrentScope.DeclareVariable(name, value);
                } else if (!value.IsUnknown()) {
                    CurrentScope.DeclareVariable(name, AstPythonMultipleMembers.Combine(existing, value));
                }
            } else {
                CurrentScope.DeclareVariable(name, value);
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

        public IMember LookupNameInScopes(string name) => LookupNameInScopes(name, DefaultLookupOptions);

        public IMember LookupNameInScopes(string name, LookupOptions options) {
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
                    if (scope.Variables.TryGetValue(name, out var value) && value != null) {
                        if (value is ILazyMember lm) {
                            value = lm.Get();
                            scope.DeclareVariable(name, value);
                        }
                        return value;
                    }
                }
            }

            if (!SuppressBuiltinLookup && options.HasFlag(LookupOptions.Builtins)) {
                return _builtinModule.Value.GetMember(name);
            }
            return null;
        }

        public void OpenScope(Node node, bool visibleToChildren = true) {
            var s = new Scope(node, CurrentScope, visibleToChildren);
            CurrentScope.AddChildScope(s);
            CurrentScope = s;
        }

        public Scope CloseScope() {
            Debug.Assert(CurrentScope.OuterScope != null, "Attempt to close global scope");
            var s = CurrentScope;
            CurrentScope = s.OuterScope as Scope;
            return s;
        }
    }
}
