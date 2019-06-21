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

using System.Collections.Generic;
using System.Linq;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Completion {
    internal static class ExpressionCompletion {
        public static IEnumerable<CompletionItem> GetCompletionsFromMembers(Expression e, IScope scope, CompletionContext context) {
            using (context.Analysis.ExpressionEvaluator.OpenScope(scope)) {
                return GetItemsFromExpression(e, context);
            }
        }

        public static IEnumerable<CompletionItem> GetCompletionsFromMembers(Expression e, ScopeStatement scope, CompletionContext context) {
            using (context.Analysis.ExpressionEvaluator.OpenScope(context.Analysis.Document, scope)) {
                return GetItemsFromExpression(e, context);
            }
        }

        private static IEnumerable<CompletionItem> GetItemsFromExpression(Expression e, CompletionContext context) {
            var eval = context.Analysis.ExpressionEvaluator;
            var value = eval.GetValueFromExpression(e);
            if (!value.IsUnknown()) {

                var type = value.GetPythonType();
                if(type is IPythonClassType cls) {
                    return GetClassItems(cls, e, context);
                }

                var items = new List<CompletionItem>();
                foreach (var t in type.GetMemberNames().ToArray()) {
                    var m = type.GetMember(t);
                    if (m is IVariable v && v.Source != VariableSource.Declaration) {
                        continue;
                    }
                    items.Add(context.ItemSource.CreateCompletionItem(t, m, type));
                }
                return items;
            }
            return Enumerable.Empty<CompletionItem>();
        }

        private static IEnumerable<CompletionItem> GetClassItems(IPythonClassType cls, Expression e, CompletionContext context) {
            var eval = context.Analysis.ExpressionEvaluator;
            // See if we are completing on self. Note that we may be inside inner function
            // that does not necessarily have 'self' argument so we are looking beyond local
            // scope. We then check that variable type matches the class type, if any.
            var selfVariable = eval.LookupNameInScopes("self");
            var completingOnSelf = cls.Equals(selfVariable?.GetPythonType()) && e is NameExpression nex && nex.Name == "self";

            var items = new List<CompletionItem>();
            var names = cls.GetMemberNames().ToArray();

            foreach (var t in names) {
                var m = cls.GetMember(t);
                if (m is IVariable v && v.Source != VariableSource.Declaration) {
                    continue;
                }

                // If this is class member completion, unmangle private member names.
                var unmangledName = cls.UnmangleMemberName(t);
                if (!string.IsNullOrEmpty(unmangledName)) {
                    // Hide private variables outside of the class scope.
                    if (!completingOnSelf && cls.IsPrivateMember(t)) {
                        continue;
                    }
                    items.Add(context.ItemSource.CreateCompletionItem(unmangledName, m, cls));
                } else {
                    items.Add(context.ItemSource.CreateCompletionItem(t, m, cls));
                }
            }
            return items;
        }
    }
}
