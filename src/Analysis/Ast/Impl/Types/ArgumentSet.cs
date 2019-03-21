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
using System.Linq;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Analyzer.Evaluation;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Extensions;
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
        private readonly ExpressionEval _eval;
        private bool _evaluated;

        public static IArgumentSet Empty = new ArgumentSet();

        public IReadOnlyList<IArgument> Arguments => _arguments;
        public IListArgument ListArgument => _listArgument;
        public IDictionaryArgument DictionaryArgument => _dictArgument;
        public IReadOnlyList<DiagnosticsEntry> Errors => _errors;
        public int OverloadIndex { get; }
        public IExpressionEvaluator Eval => _eval;


        private ArgumentSet() { }

        public ArgumentSet(IReadOnlyList<IPythonType> typeArgs) {
            _arguments = typeArgs.Select(t => new Argument(t, null)).ToList();
            _evaluated = true;
        }

        public ArgumentSet(IReadOnlyList<IMember> memberArgs) {
            _arguments = memberArgs.Select(t => new Argument(t, null)).ToList();
            _evaluated = true;
        }

        public ArgumentSet(IPythonFunctionType fn, int overloadIndex, IPythonInstance instance, CallExpression callExpr, ExpressionEval eval) :
            this(fn, overloadIndex, instance, callExpr, eval.Module, eval) { }

        /// <summary>
        /// Creates set of arguments for a function call based on the call expression
        /// and the function signature. The result contains expressions
        /// for arguments, but not actual values. <see cref="Evaluate"/> on how to
        /// get values for actual parameters.
        /// </summary>
        /// <param name="fn">Function type.</param>
        /// <param name="overloadIndex">Function overload to call.</param>
        /// <param name="instance">Type instance the function is bound to. For derived classes it is different from the declared type.</param>
        /// <param name="callExpr">Call expression that invokes the function.</param>
        /// <param name="module">Module that contains the call expression.</param>
        /// <param name="eval">Evaluator that can calculate values of arguments from their respective expressions.</param>
        public ArgumentSet(IPythonFunctionType fn, int overloadIndex, IPythonInstance instance, CallExpression callExpr, IPythonModule module, ExpressionEval eval) {
            _eval = eval;
            OverloadIndex = overloadIndex;

            var overload = fn.Overloads[overloadIndex];
            var fd = overload.FunctionDefinition;

            if (fd == null || fn.IsSpecialized) {
                // Typically specialized function, like TypeVar() that does not actually have AST definition.
                // Make the arguments from the call expression. If argument does not have name, 
                // try using name from the function definition based on the argument position.
                _arguments = new List<Argument>();
                for (var i = 0; i < callExpr.Args.Count; i++) {
                    var name = callExpr.Args[i].Name;
                    if (string.IsNullOrEmpty(name)) {
                        name = fd != null && i < fd.Parameters.Length ? fd.Parameters[i].Name : null;
                    }
                    name = name ?? $"arg{i}";
                    var parameter = fd != null && i < fd.Parameters.Length ? fd.Parameters[i] : null;
                    _arguments.Add(new Argument(name, ParameterKind.Normal, callExpr.Args[i].Expression, null, module, parameter));
                }
                return;
            }

            if (callExpr == null) {
                // Typically invoked by specialization code without call expression in the code.
                // Caller usually does not care about arguments.
                _evaluated = true;
                return;
            }
            var callLocation = callExpr.GetLocation(module);

            // https://www.python.org/dev/peps/pep-3102/#id5
            // For each formal parameter, there is a slot which will be used to contain
            // the value of the argument assigned to that parameter. Slots which have
            // had values assigned to them are marked as 'filled'.Slots which have
            // no value assigned to them yet are considered 'empty'.

            var slots = fd.Parameters.Select(p => new Argument(p, module)).ToArray();
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

            _listArgument = sa.Length == 1 && sa[0].Name.Length > 0 ? new ListArg(sa[0].Name, sa[0].ValueExpression, module, sa[0].Definition) : null;
            _dictArgument = da.Length == 1 ? new DictArg(da[0].Name, da[0].ValueExpression, module, da[0].Definition) : null;

            // Class methods
            var formalParamIndex = 0;
            if (fn.DeclaringType != null && fn.HasClassFirstArgument() && slots.Length > 0) {
                slots[0].Value = instance != null ? instance.GetPythonType() : fn.DeclaringType;
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

                    if (formalParamIndex >= fd.Parameters.Length) {
                        // We ran out of formal parameters and yet haven't seen
                        // any sequence or dictionary ones. This looks like an error.
                        _errors.Add(new DiagnosticsEntry(Resources.Analysis_TooManyFunctionArguments, arg.GetLocation(module).Span,
                            ErrorCodes.TooManyFunctionArguments, Severity.Warning));
                        return;
                    }

                    var formalParam = fd.Parameters[formalParamIndex];
                    if (formalParam.IsList) {
                        if (string.IsNullOrEmpty(formalParam.Name)) {
                            // If the next unfilled slot is a vararg slot, and it does not have a name, then it is an error.
                            _errors.Add(new DiagnosticsEntry(Resources.Analysis_TooManyPositionalArgumentBeforeStar, arg.GetLocation(module).Span,
                                ErrorCodes.TooManyPositionalArgumentsBeforeStar, Severity.Warning));
                            return;
                        }

                        // If the next unfilled slot is a vararg slot then all remaining
                        // non-keyword arguments are placed into the vararg slot.
                        if (_listArgument == null) {
                            _errors.Add(new DiagnosticsEntry(Resources.Analysis_TooManyFunctionArguments, arg.GetLocation(module).Span,
                                ErrorCodes.TooManyFunctionArguments, Severity.Warning));
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

                    if (formalParam.IsDictionary) {
                        // Next slot is a dictionary slot, but we have positional arguments still.
                        _errors.Add(new DiagnosticsEntry(Resources.Analysis_TooManyPositionalArgumentBeforeStar, arg.GetLocation(module).Span,
                            ErrorCodes.TooManyPositionalArgumentsBeforeStar, Severity.Warning));
                        return;
                    }

                    // Regular parameter
                    slots[formalParamIndex].ValueExpression = arg.Expression;
                }

                // Keyword arguments
                for (; callParamIndex < callExpr.Args.Count; callParamIndex++) {
                    var arg = callExpr.Args[callParamIndex];

                    if (string.IsNullOrEmpty(arg.Name)) {
                        _errors.Add(new DiagnosticsEntry(Resources.Analysis_PositionalArgumentAfterKeyword, arg.GetLocation(module).Span,
                            ErrorCodes.PositionalArgumentAfterKeyword, Severity.Warning));
                        return;
                    }

                    var nvp = slots.FirstOrDefault(s => s.Name.EqualsOrdinal(arg.Name));
                    if (nvp == null) {
                        // 'def f(a, b)' and then 'f(0, c=1)'. Per spec:
                        // if there is a 'keyword dictionary' argument, the argument is added
                        // to the dictionary using the keyword name as the dictionary key,
                        // unless there is already an entry with that key, in which case it is an error.
                        if (_dictArgument == null) {
                            _errors.Add(new DiagnosticsEntry(Resources.Analysis_UnknownParameterName, arg.GetLocation(module).Span,
                                ErrorCodes.UnknownParameterName, Severity.Warning));
                            return;
                        }

                        if (_dictArgument.Arguments.ContainsKey(arg.Name)) {
                            _errors.Add(new DiagnosticsEntry(Resources.Analysis_ParameterAlreadySpecified.FormatUI(arg.Name), arg.GetLocation(module).Span,
                                ErrorCodes.ParameterAlreadySpecified, Severity.Warning));
                            return;
                        }

                        _dictArgument._Expressions[arg.Name] = arg.Expression;
                        continue;
                    }

                    if (nvp.ValueExpression != null || nvp.Value != null) {
                        // Slot is already filled.
                        _errors.Add(new DiagnosticsEntry(Resources.Analysis_ParameterAlreadySpecified.FormatUI(arg.Name), arg.GetLocation(module).Span,
                            ErrorCodes.ParameterAlreadySpecified, Severity.Warning));
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
                        var parameter = fd.Parameters.First(p => p.Name == slot.Name);
                        if (parameter.DefaultValue == null) {
                            // TODO: parameter is not assigned and has no default value.
                            _errors.Add(new DiagnosticsEntry(Resources.Analysis_ParameterMissing.FormatUI(slot.Name), callLocation.Span,
                                ErrorCodes.ParameterMissing, Severity.Warning));
                        }

                        slot.ValueExpression = parameter.DefaultValue;
                    }
                }
            } finally {
                // Optimistically return what we gathered, even if there are errors.
                _arguments = slots.Where(s => s.Kind != ParameterKind.List && s.Kind != ParameterKind.Dictionary).ToList();
            }
        }

        public ArgumentSet Evaluate() {
            if (_evaluated || Eval == null) {
                return this;
            }

            foreach (var a in _arguments.Where(x => x.Value == null)) {
                a.Value = Eval.GetValueFromExpression(a.ValueExpression) ?? _eval.UnknownType;
                a.Type = Eval.GetValueFromExpression(a.TypeExpression) as IPythonType;
            }

            if (_listArgument != null) {
                foreach (var e in _listArgument.Expressions) {
                    var value = Eval.GetValueFromExpression(e) ?? _eval.UnknownType;
                    _listArgument._Values.Add(value);
                }
            }

            if (_dictArgument != null) {
                foreach (var e in _dictArgument.Expressions) {
                    var value = Eval.GetValueFromExpression(e.Value) ?? _eval.UnknownType;
                    _dictArgument._Args[e.Key] = value;
                }
            }

            _evaluated = true;
            return this;
        }

        private sealed class Argument : LocatedMember, IArgument {
            public string Name { get; }
            public object Value { get; internal set; }
            public ParameterKind Kind { get; }
            public Expression ValueExpression { get; set; }
            public IPythonType Type { get; internal set; }
            public Expression TypeExpression { get; }

            public Argument(Parameter p, IPythonModule declaringModule) :
                this(p.Name, p.Kind, null, p.Annotation, declaringModule, p) { }

            public Argument(string name, ParameterKind kind, Expression valueValueExpression, Expression typeExpression, IPythonModule declaringModule, Node definition)
                : base(declaringModule, definition) {
                Name = name;
                Kind = kind;
                ValueExpression = valueValueExpression;
                TypeExpression = typeExpression;
            }

            public Argument(IPythonType type, IPythonModule declaringModule, Node location)
                : this(type.Name, type, declaringModule, location) { }
            public Argument(IMember member, IPythonModule declaringModule, Node location)
                : this(string.Empty, member, declaringModule, location) { }

            private Argument(string name, object value, IPythonModule declaringModule, Node location)
                : base(declaringModule, location) {
                Name = name;
                Value = value;
            }
        }

        private sealed class ListArg : LocatedMember, IListArgument {
            public string Name { get; }
            public Expression Expression { get; }

            public IReadOnlyList<IMember> Values => _Values;
            public IReadOnlyList<Expression> Expressions => _Expressions;

            public List<IMember> _Values { get; } = new List<IMember>();
            public List<Expression> _Expressions { get; } = new List<Expression>();

            public ListArg(string name, Expression expression, IPythonModule declaringModule, Node location)
                : base(declaringModule, location) {
                Name = name;
                Expression = expression;
            }
        }

        private sealed class DictArg : LocatedMember, IDictionaryArgument {
            public string Name { get; }
            public Expression Expression { get; }

            public IReadOnlyDictionary<string, IMember> Arguments => _Args;
            public IReadOnlyDictionary<string, Expression> Expressions => _Expressions;

            public Dictionary<string, IMember> _Args { get; } = new Dictionary<string, IMember>();
            public Dictionary<string, Expression> _Expressions { get; } = new Dictionary<string, Expression>();

            public DictArg(string name, Expression expression, IPythonModule declaringModule, Node location)
                : base(declaringModule, location) {
                Name = name;
                Expression = expression;
            }
        }
    }
}
