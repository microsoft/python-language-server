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
using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using Microsoft.Python.LanguageServer.Protocol;
using static Microsoft.Python.Analysis.Tests.FluentAssertions.AssertionsUtilities;

namespace Microsoft.Python.LanguageServer.Tests.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal sealed class CompletionItemAssertions : ReferenceTypeAssertions<CompletionItem, CompletionItemAssertions> {
        public CompletionItemAssertions(CompletionItem subject) {
            Subject = subject;
        }

        protected override string Identifier => nameof(CompletionItem);

        [CustomAssertion]
        public AndConstraint<CompletionItemAssertions> HaveInsertText(string insertText, string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(string.Equals(Subject.insertText, insertText, StringComparison.Ordinal))
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected '{Subject.label}' completion to have insert text '{DoubleEscape(insertText)}'{{reason}}, but it has '{DoubleEscape(Subject.insertText)}'");

            return new AndConstraint<CompletionItemAssertions>(this);
        }

        [CustomAssertion]
        public AndConstraint<CompletionItemAssertions> HaveInsertTextFormat(InsertTextFormat insertTextFormat, string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(Subject.insertTextFormat == insertTextFormat)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected '{Subject.label}' completion to have insert text format '{insertTextFormat}'{{reason}}, but it has '{Subject.insertTextFormat}'");

            return new AndConstraint<CompletionItemAssertions>(this);
        }

        [CustomAssertion]
        public AndConstraint<CompletionItemAssertions> HaveDocumentation(string documentation, string because = "", params object[] reasonArgs) {
            Execute.Assertion.BecauseOf(because, reasonArgs)
                .AssertIsNotNull(Subject.documentation, $"'{Subject.label}' completion", "documentation", "\'CompletionItem.documentation\'")
                .Then
                .ForCondition(string.Equals(Subject.documentation.value, documentation, StringComparison.Ordinal))
                .FailWith($"Expected '{Subject.label}' completion to have documentation '{documentation}'{{reason}}, but it has '{Subject.documentation.value}'");

            return new AndConstraint<CompletionItemAssertions>(this);
        }
    }
}
