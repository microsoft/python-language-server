// Python Tools for Visual Studio
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

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;
using ErrorCodes = Microsoft.Python.Analysis.Diagnostics.ErrorCodes;

namespace Microsoft.Python.Analysis.Types {
    /// <summary>
    /// Set of arguments for a function call.
    /// </summary>
    internal sealed class ArgumentSet : IArgumentSet {
        private readonly List<Argument> _arguments = new List<Argument>();
        private readonly List<DiagnosticsEntry> _errors = new List<DiagnosticsEntry>();
        private readonly ListArg _listArgument;
        private readonly DictArg _dictArgument;
        private bool _evaluated;

        public static IArgumentSet WithoutContext = new ArgumentSet();

        /// <summary>Module that declares the function</summary>
        public IPythonModule DeclaringModule { get; }
        public IReadOnlyList<IArgument> Arguments => _arguments;
        public IListArgument ListArgument => _listArgument;
        public IDictionaryArgument DictionaryArgument => _dictArgument;
        public IReadOnlyList<DiagnosticsEntry> Errors => _errors;
        public int OverloadIndex { get; }
        public IExpressionEvaluator Eval { get; }

        public Expression Expression { get; }

        private ArgumentSet() { }

        /// <summary>
        /// Creates an empty argument set with some context in how the argument set was used.
        /// </summary>
        /// <param name="expr">Expression associated with argument set.</param>
        /// <param name="eval">Evaluator for the expression involving the argument set.</param>
        /// <returns></returns>
        public static ArgumentSet Empty(Expression expr, IExpressionEvaluator eval) {
            return new ArgumentSet(new List<IMember>(), expr, eval);
        }

        /// <summary>
        /// Creates a set of arguments for a call.
        /// </summary>
        /// <param name="args">Arguments for the call.</param>
        /// <param name="expr">Expression for the call.</param>
        /// <param name="eval">Evaluator of the current analysis of arguments are to be evaluated.
        /// Can be null if arguments are already known.</param>
        public ArgumentSet(IReadOnlyList<IMember> args, Expression expr, IExpressionEvaluator eval) {
            _arguments = args.Select(t => new Argument(t)).ToList();
            Expression = expr;
            Eval = eval;
            _evaluated = true;
        }

        /// <summary>
        /// Creates set of arguments for a function call based on the call expression
        /// and the function signature. The result contains expressions
        /// for arguments, but not actual values. <see cref="Evaluate"/> on how to
        /// get values for actual parameters.
        /// </summary>
        /// <param name="fn">Function type.</param>
        /// <param name="overloadIndex">Function overload to call.</param>
        /// <param name="instanceType">Type of the instance the function is bound to. For derived classes it is different from the declared type.</param>
        /// <param name="callExpr">Call expression that invokes the function.</param>
        /// <param name="eval">Evaluator that can calculate values of arguments from their respective expressions.</param>
        public ArgumentSet(IPythonFunctionType fn, int overloadIndex, IPythonType instanceType, CallExpression callExpr, IExpressionEvaluator eval) {
            Eval = eval;
            OverloadIndex = overloadIndex;
            DeclaringModule = fn.DeclaringModule;
            Expression = callExpr;

            if (callExpr == null) {
                // Typically invoked by specialization code without call expression in the code.
                // Caller usually does not care about arguments.
                _evaluated = true;
                return;
            }

            var overload = fn.Overloads[overloadIndex];
            var fdParameters = overload.FunctionDefinition?.Parameters.Where(p => !p.IsPositionalOnlyMarker).ToArray();

            // Some specialized functions have more complicated definitions, so we pass
            // parameters to those, TypeVar() is an example, so we allow the latter logic to handle
            // argument instatiation. For simple specialized functions, it is enough to handle here.
            if (fn.IsSpecialized && overload.Parameters.Count == 0) {
                // Specialized functions typically don't have AST definitions.
                // We construct the arguments from the call expression. If an argument does not have a name, 
                // we try using name from the function definition based on the argument's position.
                _arguments = new List<Argument>();
                for (var i = 0; i < callExpr.Args.Count; i++) {
                    var name = callExpr.Args[i].Name;
                    if (string.IsNullOrEmpty(name)) {
                        name = i < overload.Parameters.Count ? overload.Parameters[i].Name : $"arg{i}";
                    }
                    var node = fdParameters?.ElementAtOrDefault(i);
                    _arguments.Add(new Argument(name, ParameterKind.Normal, callExpr.Args[i].Expression, null, node));
                }
                return;
            }

            var callLocation = callExpr.Target?.GetLocation(eval);

            // https://www.python.org/dev/peps/pep-3102/#id5
            // For each formal parameter, there is a slot which will be used to contain
            // the value of the argument assigned to that parameter. Slots which have
            // had values assigned to them are marked as 'filled'.Slots which have
            // no value assigned to them yet are considered 'empty'.

            var slots = new Argument[overload.Parameters.Count];
            for (var i = 0; i < overload.Parameters.Count; i++) {
                var node = fdParameters?.ElementAtOrDefault(i);
                slots[i] = new Argument(overload.Parameters[i], node);
            }

            // Locate sequence argument, if any
            var sa = slots.Where(s => s.Kind == ParameterKind.List).ToArray();
            if (sa.Length > 1) {
                // Error should have been reported at the function definition location by the parser.
                return;
            }

            var da = slots.Where(s => s.Kind == ParameterKind.Dictionary).ToArray();
            if (da.Length > 1) {
                // Error should have been reported at the function definition location by the parser.
                return;
            }

            _listArgument = sa.Length == 1 && sa[0].Name.Length > 0 ? new ListArg(sa[0].Name, sa[0].ValueExpression, sa[0].Location) : null;
            _dictArgument = da.Length == 1 ? new DictArg(da[0].Name, da[0].ValueExpression, da[0].Location) : null;

            // Class methods
            var formalParamIndex = 0;
            if (fn.DeclaringType != null && fn.HasClassFirstArgument() && slots.Length > 0) {
                slots[0].Value = instanceType ?? fn.DeclaringType;
                formalParamIndex++;
            }

            try {
                // Positional arguments
                var callParamIndex = 0;
                for (; callParamIndex < callExpr.Args.Count; callParamIndex++, formalParamIndex++) {
                    var arg = callExpr.Args[callParamIndex];

                    if (!string.IsNullOrEmpty(arg.Name) && !arg.Name.StartsWithOrdinal("**")) {
                        // Keyword argument. Done with positionals.
                        break;
                    }

                    if (formalParamIndex >= overload.Parameters.Count) {
                        // We ran out of formal parameters and yet haven't seen
                        // any sequence or dictionary ones. This looks like an error.
                        _errors.Add(new DiagnosticsEntry(Resources.Analysis_TooManyFunctionArguments, callLocation.Span,
                            ErrorCodes.TooManyFunctionArguments, Severity.Warning, DiagnosticSource.Analysis));
                        return;
                    }

                    var formalParam = overload.Parameters[formalParamIndex];
                    if (formalParam.Kind == ParameterKind.List) {
                        if (string.IsNullOrEmpty(formalParam.Name)) {
                            // If the next unfilled slot is a vararg slot, and it does not have a name, then it is an error.
                            _errors.Add(new DiagnosticsEntry(Resources.Analysis_TooManyPositionalArgumentBeforeStar, arg.GetLocation(eval).Span,
                                ErrorCodes.TooManyPositionalArgumentsBeforeStar, Severity.Warning, DiagnosticSource.Analysis));
                            return;
                        }

                        // If the next unfilled slot is a vararg slot then all remaining
                        // non-keyword arguments are placed into the vararg slot.
                        if (_listArgument == null) {
                            _errors.Add(new DiagnosticsEntry(Resources.Analysis_TooManyFunctionArguments, arg.GetLocation(eval).Span,
                                ErrorCodes.TooManyFunctionArguments, Severity.Warning, DiagnosticSource.Analysis));
                            return;
                        }

                        for (; callParamIndex < callExpr.Args.Count; callParamIndex++) {
                            arg = callExpr.Args[callParamIndex];
                            if (!string.IsNullOrEmpty(arg.Name)) {
                                // Keyword argument. Done here.
                                break;
                            }

                            _listArgument._Expressions.Add(arg.Expression);
                        }

                        break; // Sequence or dictionary parameter found. Done here.
                    }

                    if (formalParam.Kind == ParameterKind.Dictionary) {
                        // Next slot is a dictionary slot, but we have positional arguments still.
                        _errors.Add(new DiagnosticsEntry(Resources.Analysis_TooManyPositionalArgumentBeforeStar, arg.GetLocation(eval).Span,
                            ErrorCodes.TooManyPositionalArgumentsBeforeStar, Severity.Warning, DiagnosticSource.Analysis));
                        return;
                    }

                    // Regular parameter
                    slots[formalParamIndex].ValueExpression = arg.Expression;
                }

                // Keyword arguments
                for (; callParamIndex < callExpr.Args.Count; callParamIndex++) {
                    var arg = callExpr.Args[callParamIndex];

                    if (string.IsNullOrEmpty(arg.Name)) {
                        _errors.Add(new DiagnosticsEntry(Resources.Analysis_PositionalArgumentAfterKeyword, arg.GetLocation(eval).Span,
                            ErrorCodes.PositionalArgumentAfterKeyword, Severity.Warning, DiagnosticSource.Analysis));
                        return;
                    }

                    var nvp = slots.FirstOrDefault(s => s.Name.EqualsOrdinal(arg.Name));
                    if (nvp == null) {
                        // 'def f(a, b)' and then 'f(0, c=1)'. Per spec:
                        // if there is a 'keyword dictionary' argument, the argument is added
                        // to the dictionary using the keyword name as the dictionary key,
                        // unless there is already an entry with that key, in which case it is an error.
                        if (_dictArgument == null) {
                            _errors.Add(new DiagnosticsEntry(Resources.Analysis_UnknownParameterName, arg.GetLocation(eval).Span,
                                ErrorCodes.UnknownParameterName, Severity.Warning, DiagnosticSource.Analysis));
                            return;
                        }

                        if (_dictArgument.Arguments.ContainsKey(arg.Name)) {
                            _errors.Add(new DiagnosticsEntry(Resources.Analysis_ParameterAlreadySpecified.FormatUI(arg.Name), arg.GetLocation(eval).Span,
                                ErrorCodes.ParameterAlreadySpecified, Severity.Warning, DiagnosticSource.Analysis));
                            return;
                        }

                        _dictArgument._Expressions[arg.Name] = arg.Expression;
                        continue;
                    }

                    if (nvp.Kind == ParameterKind.PositionalOnly) {
                        _errors.Add(new DiagnosticsEntry(Resources.Analysis_PositionalOnlyArgumentNamed.FormatInvariant(arg.Name), arg.GetLocation(eval).Span,
                            ErrorCodes.PositionalOnlyNamed, Severity.Warning, DiagnosticSource.Analysis));
                        return;
                    }

                    if (nvp.ValueExpression != null || nvp.Value != null) {
                        // Slot is already filled.
                        _errors.Add(new DiagnosticsEntry(Resources.Analysis_ParameterAlreadySpecified.FormatUI(arg.Name), arg.GetLocation(eval).Span,
                            ErrorCodes.ParameterAlreadySpecified, Severity.Warning, DiagnosticSource.Analysis));
                        return;
                    }

                    // OK keyword parameter
                    nvp.ValueExpression = arg.Expression;
                }

                // We went through all positionals and keywords.
                // For each remaining empty slot: if there is a default value for that slot,
                // then fill the slot with the default value. If there is no default value,
                // then it is an error.
                foreach (var slot in slots.Where(s => s.Kind != ParameterKind.List && s.Kind != ParameterKind.Dictionary && s.Value == null)) {
                    if (slot.ValueExpression == null) {
                        var parameter = overload.Parameters.First(p => p.Name == slot.Name);
                        if (parameter.DefaultValue == null) {
                            // TODO: parameter is not assigned and has no default value.
                            _errors.Add(new DiagnosticsEntry(Resources.Analysis_ParameterMissing.FormatUI(slot.Name), callLocation.Span,
                                ErrorCodes.ParameterMissing, Severity.Warning, DiagnosticSource.Analysis));
                        }
                        // Note that parameter default value expression is from the function definition AST
                        // while actual argument values are from the calling file AST.
                        slot.ValueExpression = CreateExpression(parameter.Name, parameter.DefaultValueString);
                        slot.Value = parameter.DefaultValue;
                        slot.ValueIsDefault = true;
                    }
                }
            } finally {
                // Optimistically return what we gathered, even if there are errors.
                _arguments = slots.Where(s => s.Kind != ParameterKind.List && s.Kind != ParameterKind.Dictionary).ToList();
            }
        }

        public ArgumentSet Evaluate(LookupOptions lookupOptions = LookupOptions.Normal) {
            if (_evaluated || Eval == null) {
                return this;
            }

            foreach (var a in _arguments.Where(x => x.Value == null)) {
                a.Value = GetArgumentValue(a, lookupOptions);
            }

            if (_listArgument != null) {
                foreach (var e in _listArgument.Expressions) {
                    var value = Eval.GetValueFromExpression(e, lookupOptions) ?? Eval.UnknownType;
                    _listArgument._Values.Add(value);
                }
            }

            if (_dictArgument != null) {
                foreach (var e in _dictArgument.Expressions) {
                    var value = Eval.GetValueFromExpression(e.Value, lookupOptions) ?? Eval.UnknownType;
                    _dictArgument._Args[e.Key] = value;
                }
            }

            _evaluated = true;
            return this;
        }

        private IMember GetArgumentValue(Argument arg, LookupOptions lookupOptions) {
            if (arg.Value is IMember m) {
                return m;
            }
            // Evaluates expression in the specific module context. Typically used to evaluate
            // expressions representing default values of function arguments since they are
            // are defined in the function declaring module rather than in the caller context.
            if (arg.ValueExpression == null) {
                return Eval.UnknownType;
            }

            if (arg.ValueIsDefault) {
                using (Eval.OpenScope(DeclaringModule.GlobalScope)) {
                    return Eval.GetValueFromExpression(arg.ValueExpression, lookupOptions) ?? Eval.UnknownType;
                }
            }
            return Eval.GetValueFromExpression(arg.ValueExpression, lookupOptions) ?? Eval.UnknownType;
        }

        private Expression CreateExpression(string paramName, string defaultValue) {
            if (string.IsNullOrEmpty(defaultValue)) {
                return null;
            }
            using (var sr = new StringReader($"{paramName}={defaultValue}")) {
                var parser = Parser.CreateParser(sr, Eval.Interpreter.LanguageVersion, ParserOptions.Default);
                var ast = parser.ParseFile();
                if (ast.Body is SuiteStatement ste && ste.Statements.Count > 0 && ste.Statements[0] is AssignmentStatement a) {
                    return a.Right;
                }
                return null;
            }
        }

        [DebuggerDisplay("{Name}")]
        private sealed class Argument : IArgument {
            /// <summary>
            /// Argument name.
            /// </summary>
            public string Name { get; }

            /// <summary>
            /// Argument value, if known.
            /// </summary>
            public object Value { get; internal set; }

            /// <summary>
            /// Argument kind, <see cref="ParameterKind"/>.
            /// </summary>
            public ParameterKind Kind { get; }

            /// <summary>
            /// Expression that represents value of the argument in the
            /// call expression. <see cref="CallExpression"/>.
            /// </summary>
            public Expression ValueExpression { get; set; }

            /// <summary>
            /// Indicates if value is a default value expression. Default values
            /// should be evaluated in the context of the file that declared
            /// the function rather than in the caller context.
            /// </summary>
            public bool ValueIsDefault { get; set; }

            /// <summary>
            /// Type of the argument, if annotated.
            /// </summary>
            public IPythonType Type { get; }

            /// <summary>
            /// AST node that defines the argument.
            /// </summary>
            public Node Location { get; }

            public Argument(IParameterInfo p, Node location) :
                this(p.Name, p.Kind, null, p.Type, location) { }

            public Argument(string name, ParameterKind kind, Expression valueValueExpression, IPythonType type, Node location) {
                Name = name;
                Kind = kind;
                Type = type;
                ValueExpression = valueValueExpression;
                Location = location;
            }

            public Argument(IPythonType type) : this(type.Name, type) { }
            public Argument(IMember member) : this(string.Empty, member) { }

            private Argument(string name, object value) {
                Name = name;
                Value = value;
            }
        }

        private sealed class ListArg : IListArgument {
            public string Name { get; }
            public Expression Expression { get; }
            public Node Location { get; }

            public IReadOnlyList<IMember> Values => _Values;
            public IReadOnlyList<Expression> Expressions => _Expressions;

            public List<IMember> _Values { get; } = new List<IMember>();
            public List<Expression> _Expressions { get; } = new List<Expression>();

            public ListArg(string name, Expression expression, Node location) {
                Name = name;
                Expression = expression;
                Location = location;
            }
        }

        private sealed class DictArg : IDictionaryArgument {
            public string Name { get; }
            public Expression Expression { get; }
            public Node Location { get; }

            public IReadOnlyDictionary<string, IMember> Arguments => _Args;
            public IReadOnlyDictionary<string, Expression> Expressions => _Expressions;

            public Dictionary<string, IMember> _Args { get; } = new Dictionary<string, IMember>();
            public Dictionary<string, Expression> _Expressions { get; } = new Dictionary<string, Expression>();

            public DictArg(string name, Expression expression, Node location) {
                Name = name;
                Expression = expression;
                Location = location;
            }
        }
    }
}
