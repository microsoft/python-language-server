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
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;
using ErrorCodes = Microsoft.Python.Analysis.Diagnostics.ErrorCodes;

namespace Microsoft.Python.Analysis.Types {

    internal sealed class Argument {
        public string Name;
        public Expression Value;
    }

    internal sealed class SequenceArgument {
        public string Name;
        public List<Expression> Values;
    }

    internal sealed class DictArgument {
        public string Name;
        public Dictionary<string, Expression> Arguments;
    }

    internal class ArgumentSet {
        public IReadOnlyList<Argument> Arguments { get; }
        public SequenceArgument SequenceArgument { get; }
        public DictArgument DictArgument { get; }

        private ArgumentSet(IReadOnlyList<Argument> args, SequenceArgument seqArg, DictArgument dictArg) {
            Arguments = args ?? Array.Empty<Argument>();
            SequenceArgument = seqArg;
            DictArgument = dictArg;
        }

        public static bool FromArgs(FunctionDefinition fd, CallExpression callExpr, IPythonModule module, IDiagnosticsSink diagSink, out ArgumentSet argSet) {
            argSet = null;

            var callLocation = callExpr.GetLocation(module);

            // https://www.python.org/dev/peps/pep-3102/#id5
            // For each formal parameter, there is a slot which will be used to contain
            // the value of the argument assigned to that parameter. Slots which have
            // had values assigned to them are marked as 'filled'.Slots which have
            // no value assigned to them yet are considered 'empty'.

            var slots = fd.Parameters.Select(p => new Argument { Name = p.Name }).ToArray();
            if (slots.Any(s => string.IsNullOrEmpty(s.Name))) {
                // Error should have been reported at the function definition location by the parser.
                return false;
            }

            // Locate sequence argument, if any
            var sa = slots.Where(s => s.Name.Length > 1 && s.Name[0] == '*' && s.Name[1] != '*').ToArray();
            if (sa.Length > 1) {
                // Error should have been reported at the function definition location by the parser.
                return false;
            }

            var da = slots.Where(s => s.Name.StartsWithOrdinal("**")).ToArray();
            if (da.Length > 1) {
                // Error should have been reported at the function definition location by the parser.
                return false;
            }

            if (sa.Length == 1 && da.Length == 1) {
                // Error should have been reported at the function definition location by the parser.
                return false;
            }

            var sequenceArg = sa.Length == 1 ? new SequenceArgument { Name = sa[0].Name.Substring(1), Values = new List<Expression>() } : null;
            var dictArg = da.Length == 1 ? new DictArgument { Name = da[0].Name.Substring(2), Arguments = new Dictionary<string, Expression>() } : null;

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
                    diagSink.Add(Resources.Analysis_TooManyFunctionArguments, arg.GetLocation(module).Span,
                        ErrorCodes.TooManyFunctionArguments, Severity.Warning);
                    return false;
                }

                var formalParam = fd.Parameters[callParamIndex];
                if (formalParam.Name[0] == '*') {
                    if (formalParam.Name.Length == 1) {
                        // If the next unfilled slot is a vararg slot, and it does not have a name, then it is an error.
                        diagSink.Add(Resources.Analysis_TooManyPositionalArgumentBeforeStar, arg.GetLocation(module).Span,
                            ErrorCodes.TooManyPositionalArgumentsBeforeStar, Severity.Warning);
                        return false;
                    }
                    // If the next unfilled slot is a vararg slot then all remaining
                    // non-keyword arguments are placed into the vararg slot.
                    if (sequenceArg == null) {
                        diagSink.Add(Resources.Analysis_TooManyFunctionArguments, arg.GetLocation(module).Span,
                            ErrorCodes.TooManyFunctionArguments, Severity.Warning);
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

                // Regular parameter
                slots[callParamIndex].Value = arg.Expression;
            }

            // Keyword arguments
            for (callParamIndex = 0; callParamIndex < callExpr.Args.Count; callParamIndex++) {
                var arg = callExpr.Args[callParamIndex];

                if (string.IsNullOrEmpty(arg.Name)) {
                    diagSink.Add(Resources.Analysis_PositionalArgumentAfterKeyword, arg.GetLocation(module).Span, 
                        ErrorCodes.PositionalArgumentAfterKeyword, Severity.Warning);
                    return false;
                }

                var nvp = slots.FirstOrDefault(s => s.Name.EqualsOrdinal(arg.Name));
                if (nvp == null) {
                    // 'def f(a, b)' and then 'f(0, c=1)'. Per spec:
                    // if there is a 'keyword dictionary' argument, the argument is added
                    // to the dictionary using the keyword name as the dictionary key,
                    // unless there is already an entry with that key, in which case it is an error.
                    if (dictArg == null) {
                        diagSink.Add(Resources.Analysis_UnknownParameterName, arg.GetLocation(module).Span,
                            ErrorCodes.UnknownParameterName, Severity.Warning);
                        return false;
                    }

                    if (dictArg.Arguments.ContainsKey(arg.Name)) {
                        diagSink.Add(Resources.Analysis_ParameterAlreadySpecified.FormatUI(arg.Name), arg.GetLocation(module).Span,
                            ErrorCodes.ParameterAlreadySpecified, Severity.Warning);
                        return false;
                    }
                    dictArg.Arguments[arg.Name] = arg.Expression;
                    continue;
                }

                if (nvp.Value != null) {
                    // Slot is already filled.
                    diagSink.Add(Resources.Analysis_ParameterAlreadySpecified.FormatUI(arg.Name), arg.GetLocation(module).Span,
                        ErrorCodes.ParameterAlreadySpecified, Severity.Warning);
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
                if (slot.Name.StartsWith("*")) {
                    continue;
                }

                if (slot.Value == null) {
                    var parameter = fd.Parameters.First(p => p.Name == slot.Name);
                    if (parameter.DefaultValue == null) {
                        // TODO: parameter is not assigned and has no default value.
                        diagSink.Add(Resources.Analysis_ParameterMissing.FormatUI(slot.Name), callLocation.Span,
                            ErrorCodes.ParameterAlreadySpecified, Severity.Warning);
                        return false;
                    }
                    slot.Value = parameter.DefaultValue;
                }
            }

            argSet = new ArgumentSet(slots.Where(s => !s.Name.StartsWithOrdinal("*")).ToList(), sequenceArg, dictArg);
            return true;
        }
    }
}
