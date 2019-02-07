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
using System.Diagnostics.CodeAnalysis;
using Microsoft.Python.Analysis.Tests.FluentAssertions;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Completion;
using Microsoft.Python.LanguageServer.Protocol;

namespace Microsoft.Python.LanguageServer.Tests.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal static class AssertionsFactory {
        public static CompletionItemAssertions Should(this CompletionItem completionItem)
            => new CompletionItemAssertions(completionItem);

        public static CompletionResultAssertions Should(this CompletionResult completionResult)
            => new CompletionResultAssertions(completionResult);

        public static RangeAssertions Should(this Range range) => new RangeAssertions(range);
        public static RangeAssertions Should(this Range? range) => new RangeAssertions(range.Value);

        public static SignatureHelpAssertions Should(this SignatureHelp signatureHelp)
            => new SignatureHelpAssertions(signatureHelp);

        public static SignatureInformationAssertions Should(this SignatureInformation signatureInformation)
            => new SignatureInformationAssertions(signatureInformation);

        public static SourceSpanAssertions Should(this SourceSpan span) => new SourceSpanAssertions(span);
        public static SourceSpanAssertions Should(this SourceSpan? span) => new SourceSpanAssertions(span.Value);

        public static TextEditCollectionAssertions Should(this IEnumerable<TextEdit> textEdits)
            => new TextEditCollectionAssertions(textEdits);
    }
}
