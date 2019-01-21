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

using System.Linq;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Completion {
    internal static class WithCompletion {
        public static bool TryGetCompletions(WithStatement withStatement, CompletionContext context, out CompletionResult result) {
            result = null;

            if (context.Position > withStatement.HeaderIndex && withStatement.HeaderIndex > withStatement.StartIndex) {
                return false;
            }

            foreach (var item in withStatement.Items.Reverse().MaybeEnumerate()) {
                if (item.AsIndex > item.StartIndex) {
                    if (context.Position > item.AsIndex + 2) {
                        return true;
                    }

                    if (context.Position >= item.AsIndex) {
                        var applicableSpan = new SourceSpan(context.IndexToLocation(item.AsIndex), context.IndexToLocation(item.AsIndex + 2));
                        result = new CompletionResult(Enumerable.Repeat(CompletionItemSource.AsKeyword,1), applicableSpan);
                        return true;
                    }
                }

                if (item.ContextManager != null && !(item.ContextManager is ErrorExpression)) {
                    if (context.Position > item.ContextManager.EndIndex && item.ContextManager.EndIndex > item.ContextManager.StartIndex) {
                        result = result = new CompletionResult(Enumerable.Repeat(CompletionItemSource.AsKeyword, 1));
                        return true;
                    }

                    if (context.Position >= item.ContextManager.StartIndex) {
                        return false;
                    }
                }
            }

            return false;
        }
    }
}
