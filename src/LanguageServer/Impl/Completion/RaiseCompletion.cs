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
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Completion {
    internal static class RaiseCompletion {
        public static bool TryGetCompletions(RaiseStatement raiseStatement, CompletionContext context, out CompletionResult result) {
            result = null;

            // raise Type, Value, Traceback with Cause
            if (raiseStatement.Cause != null && context.Position >= raiseStatement.CauseFieldStartIndex) {
                return false;
            }

            if (raiseStatement.Traceback != null && context.Position >= raiseStatement.TracebackFieldStartIndex) {
                return false;
            }

            if (raiseStatement.Value != null && context.Position >= raiseStatement.ValueFieldStartIndex) {
                return false;
            }

            if (raiseStatement.ExceptType == null) {
                return false;
            }

            if (context.Position <= raiseStatement.ExceptType.EndIndex) {
                return false;
            }

            if (context.Ast.LanguageVersion.Is3x()) {
                var applicableSpan = context.GetApplicableSpanFromLastToken(raiseStatement);
                result = new CompletionResult(Enumerable.Repeat(CompletionItemSource.FromKeyword, 1), applicableSpan);
            }

            return true;
        }
    }
}
