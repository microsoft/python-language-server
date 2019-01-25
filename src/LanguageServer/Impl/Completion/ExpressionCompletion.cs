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
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Completion {
    internal static  class ExpressionCompletion {
        public static Task<IEnumerable<CompletionItem>> GetCompletionsFromMembersAsync(Expression e, IScope scope, CompletionContext context, CancellationToken cancellationToken = default) {
            using (context.Analysis.ExpressionEvaluator.OpenScope(scope)) {
                return GetItemsFromExpressionAsync(e, context, cancellationToken);
            }
        }

        public static Task<IEnumerable<CompletionItem>> GetCompletionsFromMembersAsync(Expression e, ScopeStatement scope, CompletionContext context, CancellationToken cancellationToken = default) {
            using (context.Analysis.ExpressionEvaluator.OpenScope(scope)) {
                return GetItemsFromExpressionAsync(e, context, cancellationToken);
            }
        }

        private static async Task<IEnumerable<CompletionItem>> GetItemsFromExpressionAsync(Expression e, CompletionContext context, CancellationToken cancellationToken = default) {
            var value = await context.Analysis.ExpressionEvaluator.GetValueFromExpressionAsync(e, cancellationToken);
            if (!value.IsUnknown()) {
                var items = new List<CompletionItem>();
                var type = value.GetPythonType();
                var names = type.GetMemberNames().ToArray();
                foreach (var t in names) {
                    var m = type.GetMember(t);
                    if(m is IVariable v && v.Source != VariableSource.Declaration) {
                        continue;
                    }
                    items.Add(context.ItemSource.CreateCompletionItem(t, m));
                }
                return items;
            }
            return Enumerable.Empty<CompletionItem>();
        }
    }
}
