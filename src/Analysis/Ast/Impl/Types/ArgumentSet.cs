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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Analyzer.Evaluation;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;
using ErrorCodes = Microsoft.Python.Analysis.Diagnostics.ErrorCodes;

namespace Microsoft.Python.Analysis.Types {

    internal sealed class Argument<T> {
        public string Name;
        public ParameterKind Kind;
        public T Value;
    }

    internal sealed class SequenceArgument<T> {
        public string Name;
        public List<T> Values;
    }

    internal sealed class DictArgument<T> {
        public string Name;
        public Dictionary<string, T> Arguments;
    }

    internal class ArgumentSet<T> {
        public IReadOnlyList<Argument<T>> Arguments { get; }
        public SequenceArgument<T> SequenceArgument { get; }
        public DictArgument<T> DictArgument { get; }
        public IReadOnlyList<DiagnosticsEntry> Errors { get; }

        private ArgumentSet(IReadOnlyList<Argument<T>> args, SequenceArgument<T> seqArg, DictArgument<T> dictArg, IReadOnlyList<DiagnosticsEntry> errors) {
            Arguments = args ?? Array.Empty<Argument<T>>();
            SequenceArgument = seqArg;
            DictArgument = dictArg;
            Errors = errors;
        }

        public static bool FromArgs(FunctionDefinition fd, CallExpression callExpr, IPythonModule module, out ArgumentSet<Expression> argSet) {
            var callLocation = callExpr.GetLocation(module);
            var errors = new List<DiagnosticsEntry>();
            argSet = new ArgumentSet<Expression>(Array.Empty<Argument<Expression>>(), null, null, errors);

            // https://www.python.org/dev/peps/pep-3102/#id5
            // For each formal parameter, there is a slot which will be used to contain
            // the value of the argument assigned to that parameter. Slots which have
            // had values assigned to them are marked as 'filled'.Slots which have
            // no value assigned to them yet are considered 'empty'.

            var slots = fd.Parameters.Select(p => new Argument<Expression> { Name = p.Name, Kind = p.Kind }).ToArray();

            // Locate sequence argument, if any
            var sa = slots.Where(s => s.Kind == ParameterKind.List).ToArray();
            if (sa.Length > 1) {
                // Error should have been reported at the function definition location by the parser.
                return false;
            }

            var da = slots.Where(s => s.Kind == ParameterKind.Dictionary).ToArray();
            if (da.Length > 1) {
                // Error should have been reported at the function definition location by the parser.
                return false;
            }

            if (sa.Length == 1 && da.Length == 1) {
                // Error should have been reported at the function definition location by the parser.
                return false;
            }

            var sequenceArg = sa.Length == 1 && sa[0].Name.Length > 0
                ? new SequenceArgument<Expression> { Name = sa[0].Name, Values = new List<Expression>() } : null;
            var dictArg = da.Length == 1
                ? new DictArgument<Expression> { Name = da[0].Name, Arguments = new Dictionary<string, Expression>() } : null;

            // Positional arguments
            var callParamIndex = 0;
            for (; callParamIndex < callExpr.Args.Count; callParamIndex++) {
                var arg = callExpr.Args[callParamIndex];

                if (!string.IsNullOrEmpty(arg.Name)) {
                    // Keyword argument. Done with positionals.
                    break;
                }

                if (callParamIndex >= fd.Parameters.Length) {
                    // We ran out of formal parameters and yet haven't seen
                    // any sequence or dictionary ones. This looks like an error.
                    errors.Add(new DiagnosticsEntry(Resources.Analysis_TooManyFunctionArguments, arg.GetLocation(module).Span,
                        ErrorCodes.TooManyFunctionArguments, Severity.Warning));
                    return false;
                }

                var formalParam = fd.Parameters[callParamIndex];
                if (formalParam.IsList) {
                    if (string.IsNullOrEmpty(formalParam.Name)) {
                        // If the next unfilled slot is a vararg slot, and it does not have a name, then it is an error.
                        errors.Add(new DiagnosticsEntry(Resources.Analysis_TooManyPositionalArgumentBeforeStar, arg.GetLocation(module).Span,
                            ErrorCodes.TooManyPositionalArgumentsBeforeStar, Severity.Warning));
                        return false;
                    }
                    // If the next unfilled slot is a vararg slot then all remaining
                    // non-keyword arguments are placed into the vararg slot.
                    if (sequenceArg == null) {
                        errors.Add(new DiagnosticsEntry(Resources.Analysis_TooManyFunctionArguments, arg.GetLocation(module).Span,
                            ErrorCodes.TooManyFunctionArguments, Severity.Warning));
                        return false;
                    }

                    for (; callParamIndex < callExpr.Args.Count; callParamIndex++) {
                        arg = callExpr.Args[callParamIndex];
                        if (!string.IsNullOrEmpty(arg.Name)) {
                            // Keyword argument. Done here.
                            break;
                        }
                        sequenceArg.Values.Add(arg.Expression);
                    }
                    break; // Sequence or dictionary parameter found. Done here.
                }

                if (formalParam.IsDictionary) {
                    // Next slot is a dictionary slot, but we have positional arguments still.
                    errors.Add(new DiagnosticsEntry(Resources.Analysis_TooManyPositionalArgumentBeforeStar, arg.GetLocation(module).Span,
                        ErrorCodes.TooManyPositionalArgumentsBeforeStar, Severity.Warning));
                    return false;
                }

                // Regular parameter
                slots[callParamIndex].Value = arg.Expression;
            }

            // Keyword arguments
            for (; callParamIndex < callExpr.Args.Count; callParamIndex++) {
                var arg = callExpr.Args[callParamIndex];

                if (string.IsNullOrEmpty(arg.Name)) {
                    errors.Add(new DiagnosticsEntry(Resources.Analysis_PositionalArgumentAfterKeyword, arg.GetLocation(module).Span,
                        ErrorCodes.PositionalArgumentAfterKeyword, Severity.Warning));
                    return false;
                }

                var nvp = slots.FirstOrDefault(s => s.Name.EqualsOrdinal(arg.Name));
                if (nvp == null) {
                    // 'def f(a, b)' and then 'f(0, c=1)'. Per spec:
                    // if there is a 'keyword dictionary' argument, the argument is added
                    // to the dictionary using the keyword name as the dictionary key,
                    // unless there is already an entry with that key, in which case it is an error.
                    if (dictArg == null) {
                        errors.Add(new DiagnosticsEntry(Resources.Analysis_UnknownParameterName, arg.GetLocation(module).Span,
                            ErrorCodes.UnknownParameterName, Severity.Warning));
                        return false;
                    }

                    if (dictArg.Arguments.ContainsKey(arg.Name)) {
                        errors.Add(new DiagnosticsEntry(Resources.Analysis_ParameterAlreadySpecified.FormatUI(arg.Name), arg.GetLocation(module).Span,
                            ErrorCodes.ParameterAlreadySpecified, Severity.Warning));
                        return false;
                    }
                    dictArg.Arguments[arg.Name] = arg.Expression;
                    continue;
                }

                if (nvp.Value != null) {
                    // Slot is already filled.
                    errors.Add(new DiagnosticsEntry(Resources.Analysis_ParameterAlreadySpecified.FormatUI(arg.Name), arg.GetLocation(module).Span,
                        ErrorCodes.ParameterAlreadySpecified, Severity.Warning));
                    return false;
                }

                // OK keyword parameter
                nvp.Value = arg.Expression;
            }

            // We went through all positionals and keywords.
            // For each remaining empty slot: if there is a default value for that slot,
            // then fill the slot with the default value. If there is no default value,
            // then it is an error.
            foreach (var slot in slots) {
                if (slot.Kind == ParameterKind.List || slot.Kind == ParameterKind.Dictionary) {
                    continue;
                }

                if (slot.Value == null) {
                    var parameter = fd.Parameters.First(p => p.Name == slot.Name);
                    if (parameter.DefaultValue == null) {
                        // TODO: parameter is not assigned and has no default value.
                        errors.Add(new DiagnosticsEntry(Resources.Analysis_ParameterMissing.FormatUI(slot.Name), callLocation.Span,
                            ErrorCodes.ParameterMissing, Severity.Warning));
                    }
                    slot.Value = parameter.DefaultValue;
                }
            }

            if (errors.Count == 0) {
                var regularArgs = slots.Where(s => s.Kind != ParameterKind.List && s.Kind != ParameterKind.Dictionary).ToList();
                argSet = new ArgumentSet<Expression>(regularArgs, sequenceArg, dictArg, errors);
            }
            return errors.Count == 0;
        }

        public async Task<ArgumentSet<IMember>> EvaluateAsync(ArgumentSet<Expression> exprArgSet, ExpressionEval eval, CancellationToken cancellationToken = default) {
            var args = new List<Argument<IMember>>();

            foreach (var a in exprArgSet.Arguments) {
                var value = await eval.GetValueFromExpressionAsync(a.Value, cancellationToken);
                args.Add(new Argument<IMember> { Name = a.Name, Kind = a.Kind, Value = value });
            }

            SequenceArgument<IMember> sequenceArg = null;
            if (exprArgSet.SequenceArgument != null) {
                var values = new List<IMember>();
                foreach (var v in exprArgSet.SequenceArgument.Values) {
                    var value = await eval.GetValueFromExpressionAsync(v, cancellationToken);
                    values.Add(value);
                }
                sequenceArg = new SequenceArgument<IMember> { Name = exprArgSet.SequenceArgument.Name, Values = values };
            }

            DictArgument<IMember> dictArg = null;
            if (exprArgSet.DictArgument != null) {
                var arguments = new Dictionary<string, IMember>();
                foreach (var a in exprArgSet.DictArgument.Arguments) {
                    var value = await eval.GetValueFromExpressionAsync(a.Value, cancellationToken);
                    arguments[a.Key] = value;
                }
                dictArg = new DictArgument<IMember> { Name = exprArgSet.DictArgument.Name, Arguments = arguments };
            }

            return new ArgumentSet<IMember>(args, sequenceArg, dictArg, exprArgSet.Errors);
        }
    }
}
