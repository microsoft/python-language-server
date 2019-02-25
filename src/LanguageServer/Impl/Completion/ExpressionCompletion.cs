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
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Completion {
    internal static  class ExpressionCompletion {
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
            var value = context.Analysis.ExpressionEvaluator.GetValueFromExpression(e);
            if (!value.IsUnknown()) {
                var items = new List<CompletionItem>();
                var type = value.GetPythonType();
                var names = type.GetMemberNames().ToArray();
                foreach (var t in names) {
                    var m = type.GetMember(t);
                    if(m is IVariable v && v.Source != VariableSource.Declaration) {
                        continue;
                    }
                    items.Add(context.ItemSource.CreateCompletionItem(t, m, type));
                }
                return items;
            }
            return Enumerable.Empty<CompletionItem>();
        }
    }
}
