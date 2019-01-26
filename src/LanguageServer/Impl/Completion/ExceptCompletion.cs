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
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Completion {
    internal static class ExceptCompletion {
        public static bool TryGetCompletions(TryStatementHandler tryStatement, CompletionContext context, out CompletionResult result) {
            result = CompletionResult.Empty;

            // except Test as Target
            if (tryStatement.Target != null && context.Position >= tryStatement.Target.StartIndex) {
                return true;
            }

            if (tryStatement.Test is TupleExpression || tryStatement.Test is null) {
                return false;
            }

            if (context.Position <= tryStatement.Test.EndIndex) {
                return false;
            }

            var applicableSpan = context.GetApplicableSpanFromLastToken(tryStatement);
            result = new CompletionResult(Enumerable.Repeat(CompletionItemSource.AsKeyword, 1), applicableSpan);
            return true;
        }
    }
}
