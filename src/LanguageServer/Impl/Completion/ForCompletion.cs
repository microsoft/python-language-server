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
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Completion {
    internal static class ForCompletion {
        public static bool TryGetCompletions(ForStatement forStatement, CompletionContext context, out CompletionResult result) {
            result = null;

            if (forStatement.Left == null) {
                return false;
            }

            if (forStatement.InIndex > forStatement.StartIndex) {
                if (context.Position > forStatement.InIndex + 2) {
                    return false;
                }

                if (context.Position >= forStatement.InIndex) {
                    var applicableSpan = new SourceSpan(
                        context.IndexToLocation(forStatement.InIndex), 
                        context.IndexToLocation(forStatement.InIndex + 2)
                    );
                    result = new CompletionResult(Enumerable.Repeat(CompletionItemSource.InKeyword, 1), applicableSpan);
                    return true;
                }
            }

            if (forStatement.Left.StartIndex > forStatement.StartIndex && 
                forStatement.Left.EndIndex > forStatement.Left.StartIndex && 
                context.Position > forStatement.Left.EndIndex) {

                var applicableSpan = context.GetApplicableSpanFromLastToken(forStatement);
                result = new CompletionResult(Enumerable.Repeat(CompletionItemSource.InKeyword, 1), applicableSpan);
                return true;
            }

            return forStatement.ForIndex >= forStatement.StartIndex && context.Position > forStatement.ForIndex + 3;
        }
    }
}
