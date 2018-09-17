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
using Microsoft.Python.LanguageServer;
using static Microsoft.PythonTools.Analysis.FluentAssertions.AssertionsUtilities;

namespace Microsoft.PythonTools.Analysis.FluentAssertions {
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
    }
}