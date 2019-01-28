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
using System.Threading;
using System.Threading.Tasks;
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
        private readonly ExpressionEval _eval;
        private readonly ListArg _listArgument;
        private readonly DictArg _dictArgument;
        private bool _evaluated;

        public static IArgumentSet Empty = new ArgumentSet();

        public IReadOnlyList<IArgument> Arguments => _arguments;
        public IListArgument ListArgument => _listArgument;
        public IDictionaryArgument DictionaryArgument => _dictArgument;
        public IReadOnlyList<DiagnosticsEntry> Errors => _errors;
        public int OverloadIndex { get; }

        private ArgumentSet() { }

        public ArgumentSet(IReadOnlyList<IPythonType> types) {
            _arguments = types.Select(t => new Argument(t)).ToList();
            _evaluated = true;
        }

        public ArgumentSet(IPythonFunctionType fn, int overloadIndex, IPythonInstance instance, CallExpression callExpr, ExpressionEval eval) :
            this(fn, overloadIndex, instance, callExpr, eval.Module, eval) { }

        /// <summary>
        /// Creates set of arguments for a function call based on the call expression
        /// and the function signature. The result contains expressions
        /// for arguments, but not actual values. <see cref="EvaluateAsync"/> on how to
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
                    _arguments.Add(new Argument(name, ParameterKind.Normal) { Expression = callExpr.Args[i].Expression });
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

            var slots = fd.Parameters.Select(p => new Argument(p.Name, p.Kind)).ToArray();
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

            _listArgument = sa.Length == 1 && sa[0].Name.Length > 0 ? new ListArg(sa[0].Name) : null;
            _dictArgument = da.Length == 1 ? new DictArg(da[0].Name) : null;

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
                    slots[formalParamIndex].Expression = arg.Expression;
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

                    if (nvp.Expression != null || nvp.Value != null) {
                        // Slot is already filled.
                        _errors.Add(new DiagnosticsEntry(Resources.Analysis_ParameterAlreadySpecified.FormatUI(arg.Name), arg.GetLocation(module).Span,
                            ErrorCodes.ParameterAlreadySpecified, Severity.Warning));
                        return;
                    }

                    // OK keyword parameter
                    nvp.Expression = arg.Expression;
                }

                // We went through all positionals and keywords.
                // For each remaining empty slot: if there is a default value for that slot,
                // then fill the slot with the default value. If there is no default value,
                // then it is an error.
                foreach (var slot in slots.Where(s => s.Kind != ParameterKind.List && s.Kind != ParameterKind.Dictionary && s.Value == null)) {
                    if (slot.Expression == null) {
                        var parameter = fd.Parameters.First(p => p.Name == slot.Name);
                        if (parameter.DefaultValue == null) {
                            // TODO: parameter is not assigned and has no default value.
                            _errors.Add(new DiagnosticsEntry(Resources.Analysis_ParameterMissing.FormatUI(slot.Name), callLocation.Span,
                                ErrorCodes.ParameterMissing, Severity.Warning));
                        }

                        slot.Expression = parameter.DefaultValue;
                    }
                }
            } finally {
                // Optimistically return what we gathered, even if there are errors.
                _arguments = slots.Where(s => s.Kind != ParameterKind.List && s.Kind != ParameterKind.Dictionary).ToList();
            }
        }

        public async Task<ArgumentSet> EvaluateAsync(CancellationToken cancellationToken = default) {
            if (_evaluated || _eval == null) {
                return this;
            }

            foreach (var a in _arguments.Where(x => x.Value == null)) {
                a.Value = await _eval.GetValueFromExpressionAsync(a.Expression, cancellationToken);
            }

            if (_listArgument != null) {
                foreach (var e in _listArgument.Expressions) {
                    var value = await _eval.GetValueFromExpressionAsync(e, cancellationToken);
                    _listArgument._Values.Add(value);
                }
            }

            if (_dictArgument != null) {
                foreach (var e in _dictArgument.Expressions) {
                    var value = await _eval.GetValueFromExpressionAsync(e.Value, cancellationToken);
                    _dictArgument._Args[e.Key] = value;
                }
            }

            _evaluated = true;
            return this;
        }

        private sealed class Argument : IArgument {
            public string Name { get; }
            public object Value { get; internal set; }

            public ParameterKind Kind { get; }
            public Expression Expression { get; set; }

            public Argument(string name, ParameterKind kind) {
                Name = name;
                Kind = kind;
            }
            public Argument(IParameterInfo info) {
                Name = info.Name;
                if (info.IsKeywordDict) {
                    Kind = ParameterKind.Dictionary;
                } else if (info.IsParamArray) {
                    Kind = ParameterKind.List;
                } else if (string.IsNullOrEmpty(info.DefaultValueString)) {
                    Kind = ParameterKind.KeywordOnly;
                } else {
                    Kind = ParameterKind.Normal;
                }
            }

            public Argument(IPythonType type) {
                Value = type;
            }
        }

        private sealed class ListArg : IListArgument {
            public string Name { get; }
            public IReadOnlyList<IMember> Values => _Values;
            public IReadOnlyList<Expression> Expressions => _Expressions;

            public List<IMember> _Values { get; } = new List<IMember>();
            public List<Expression> _Expressions { get; } = new List<Expression>();

            public ListArg(string name) {
                Name = name;
            }
        }

        private sealed class DictArg : IDictionaryArgument {
            public string Name { get; }
            public IReadOnlyDictionary<string, IMember> Arguments => _Args;
            public IReadOnlyDictionary<string, Expression> Expressions => _Expressions;

            public Dictionary<string, IMember> _Args { get; } = new Dictionary<string, IMember>();
            public Dictionary<string, Expression> _Expressions { get; } = new Dictionary<string, Expression>();

            public DictArg(string name) {
                Name = name;
            }
        }
    }
}
