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
using System.Collections.Specialized;
using System.Linq;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Types {

    class NameValuePair {
        public string Name;
        public Expression Value;
    }

    internal class ArgumentSet {
        private OrderedDictionary _arguments;
        private List<Expression> _sequenceArgs;
        private Dictionary<string, Expression> _dictArgs;

        private OrderedDictionary Args => _arguments ?? (_arguments = new OrderedDictionary());
        private List<Expression> SeqArgs => _sequenceArgs ?? (_sequenceArgs = new List<Expression>());
        private Dictionary<string, Expression> DictArgs => _dictArgs ?? (_dictArgs = new Dictionary<string, Expression>());


        public IReadOnlyDictionary<string, Expression> Arguments => Args;
        public IReadOnlyList<Expression> SequenceArguments => SeqArgs?.ToArray() ?? Array.Empty<Expression>();
        public IReadOnlyDictionary<string, Expression> DictArguments => DictArgs ?? EmptyDictionary<string, Expression>.Instance;


        public static bool FromArgs(FunctionDefinition fd, CallExpression callExpr, out ArgumentSet argSet) {
            argSet = null;

            // https://www.python.org/dev/peps/pep-3102/#id5
            // For each formal parameter, there is a slot which will be used to contain
            // the value of the argument assigned to that parameter. Slots which have
            // had values assigned to them are marked as 'filled'.Slots which have
            // no value assigned to them yet are considered 'empty'.
            var slots = fd.Parameters.Select(p => new NameValuePair { Name = p.Name }).ToArray();
            if (slots.Any(s => string.IsNullOrEmpty(s.Name))) {
                // TODO: report missing formal parameter name.
                return false;
            }

            // Locate sequence argument, if any
            var sequenceArg = slots.FirstOrDefault(s => s.Name.Length > 1 && s.Name[0] == '*' && s.Name[1] != '*');
            var dictionaryArg = slots.FirstOrDefault(s => s.Name.StartsWithOrdinal("**"));

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
                    // TODO: report too many arguments
                    return false;
                }

                var formalParam = fd.Parameters[callParamIndex];
                if (formalParam.Name[0] == '*') {
                    if (formalParam.Name.Length == 1) {
                        // If the next unfilled slot is a vararg slot, and it does not have a name, then it is an error.
                        // TODO: report that '*' argument was found and yet we still have positional arguments.
                        return false;
                    }
                    break; // Sequence or dictionary parameter found. Done here.
                }

                // Regular parameter
                slots[callParamIndex].Value = arg.Expression;
            }

            // Now keyword parameters
            for (; callParamIndex < callExpr.Args.Count; callParamIndex++) {
                var arg = callExpr.Args[callParamIndex];

                if (string.IsNullOrEmpty(arg.Name)) {
                    // TODO: report that positional parameter appears after the keyword parameter.
                    return false;
                }

                var nvp = slots.FirstOrDefault(s => s.Name.EqualsOrdinal(arg.Name));
                if(nvp == null) {
                    // 'def f(a, b)' and then 'f(0, c=1)'. Per spec:
                    // if there is a 'keyword dictionary' argument, the argument is added
                    // to the dictionary using the keyword name as the dictionary key,
                    // unless there is already an entry with that key, in which case it is an error.
                    if (dictionaryArg == null) {
                        // TODO: report that parameter name is unknown.
                        return false;
                    }
                }

                if (nvp.Value != null) {
                    // Slot is already filled.
                    // TODO: duplicate parameter, such as 'def f(a, b)' and then 'f(0, a=1)'.
                    return false;
                }

                if (callParamIndex >= fd.Parameters.Length) {
                    // We ran out of formal parameters and yet haven't seen
                    // any sequence or dictionary ones. This looks like an error.
                    // TODO: report too many arguments
                    return false;
                }

                var formalParam = fd.Parameters[callParamIndex];
                if (formalParam.Name[0] == '*') {
                    break; // Sequence or dictionary parameter found. Done here.
                }

                // OK keyword parameter
                nvp.Value = arg.Expression;
            }

            // We went through all positionals and keywords.
            // Now sequence or dictionary parameters
            var fp = fd.Parameters[callParamIndex];
            if (fp.Name[0] != '*') {
                return false;
            }

            for (; callParamIndex < callExpr.Args.Count; callParamIndex++) {
                var arg = callExpr.Args[callParamIndex];

                if (string.IsNullOrEmpty(arg.Name)) {
                    // TODO: report that positional parameter appears after the keyword parameter.
                    return false;
                }

                var nvp = slots.FirstOrDefault(s => s.Name.EqualsOrdinal(arg.Name));
                if (nvp == null) {
                    // TODO: report no such named parameter'. As in 'def f(a, b)' and then 'f(0, c=1)'
                    return false;
                }
                if (nvp.Value != null) {
                    // Slot is already filled.
                    // TODO: duplicate parameter, such as 'def f(a, b)' and then 'f(0, a=1)'.
                    return false;
                }

                if (callParamIndex >= fd.Parameters.Length) {
                    // We ran out of formal parameters and yet haven't seen
                    // any sequence or dictionary ones. This looks like an error.
                    // TODO: report too many arguments
                    return false;
                }

                var formalParam = fd.Parameters[callParamIndex];
                if (formalParam.Name[0] == '*') {
                    break; // Sequence or dictionary parameter found. Done here.
                }

                // OK keyword parameter
                nvp.Value = arg.Expression;
            }
            return true;
        }
    }
}
