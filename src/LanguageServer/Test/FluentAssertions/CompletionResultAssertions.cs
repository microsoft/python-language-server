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
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using Microsoft.Python.LanguageServer.Completion;
using Microsoft.Python.LanguageServer.Protocol;
using static Microsoft.Python.Analysis.Tests.FluentAssertions.AssertionsUtilities;

namespace Microsoft.Python.LanguageServer.Tests.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal sealed class CompletionResultAssertions : ReferenceTypeAssertions<CompletionResult, CompletionResultAssertions> {
        public CompletionResultAssertions(CompletionResult subject) {
            Subject = subject;
        }

        protected override string Identifier => nameof(CompletionResult);

        [CustomAssertion]
        public AndConstraint<CompletionResultAssertions> OnlyHaveLabels(params string[] labels)
            => OnlyHaveLabels(labels, string.Empty);

        [CustomAssertion]
        public AndConstraint<CompletionResultAssertions> HaveAnyCompletions(string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);

            var errorMessage = Subject.Completions != null
                ? Subject.Completions.Count > 0 ? null : $"Expected {GetName()} to have completion items{{reason}}, but CompletionList.items collection is empty."
                : $"Expected {GetName()} to have completion items{{reason}}, but CompletionList.items collection is null.";

            Execute.Assertion.ForCondition(errorMessage == null)
                .BecauseOf(because, reasonArgs)
                .FailWith(errorMessage);

            return new AndConstraint<CompletionResultAssertions>(this);
        }

        [CustomAssertion]
        public AndConstraint<CompletionResultAssertions> HaveNoCompletion(string because = "", params object[] reasonArgs) 
            => OnlyHaveLabels(Array.Empty<string>(), because, reasonArgs);

        [CustomAssertion]
        public AndConstraint<CompletionResultAssertions> OnlyHaveLabels(IEnumerable<string> labels, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);

            var actual = Subject.Completions?.Select(i => i.label).ToArray() ?? Array.Empty<string>();
            var expected = labels.ToArray();

            var errorMessage = GetAssertCollectionOnlyContainsMessage(actual, expected, GetName(), "label", "labels");

            Execute.Assertion.ForCondition(errorMessage == null)
                .BecauseOf(because, reasonArgs)
                .FailWith(errorMessage);

            return new AndConstraint<CompletionResultAssertions>(this);
        }

        [CustomAssertion]
        public AndWhichConstraint<CompletionResultAssertions, CompletionItem> HaveItem(string label, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);

            var actual = Subject.Completions?.Where(i => string.Equals(i.label, label, StringComparison.Ordinal)).ToArray() ?? Array.Empty<CompletionItem>();
            var errorMessage = GetAssertCollectionContainsMessage(actual.Select(i => i.label).ToArray(), new [] { label }, GetName(), "label", "labels");

            Execute.Assertion.ForCondition(errorMessage == null)
                .BecauseOf(because, reasonArgs)
                .FailWith(errorMessage);

            return new AndWhichConstraint<CompletionResultAssertions, CompletionItem>(this, actual[0]);
        }

        [CustomAssertion]
        public AndConstraint<CompletionResultAssertions> HaveLabels(params string[] labels)
            => HaveLabels(labels, string.Empty);

        [CustomAssertion]
        public AndConstraint<CompletionResultAssertions> HaveLabels(IEnumerable<string> labels, string because = "", params object[] reasonArgs) 
            => HaveAttribute(labels, i => i.label, "label", "labels", because, reasonArgs);

        [CustomAssertion]
        public AndConstraint<CompletionResultAssertions> HaveInsertTexts(params string[] insertTexts)
            => HaveInsertTexts(insertTexts, string.Empty);

        [CustomAssertion]
        public AndConstraint<CompletionResultAssertions> HaveInsertTexts(IEnumerable<string> insertTexts, string because = "", params object[] reasonArgs) 
            => HaveAttribute(insertTexts, i => i.insertText, "insert text", "insert texts", because, reasonArgs);

        private AndConstraint<CompletionResultAssertions> HaveAttribute(IEnumerable<string> attributes, Func<CompletionItem, string> attributeSelector, string itemNameSingle, string itemNamePlural, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);

            var actual = Subject.Completions?.Select(attributeSelector).ToArray() ?? Array.Empty<string>();
            var expected = attributes.ToArray();

            var errorMessage = GetAssertCollectionContainsMessage(actual, expected, GetName(), itemNameSingle, itemNamePlural);

            Execute.Assertion.ForCondition(errorMessage == null)
                .BecauseOf(because, reasonArgs)
                .FailWith(errorMessage);

            return new AndConstraint<CompletionResultAssertions>(this);
        }

        [CustomAssertion]
        public AndConstraint<CompletionResultAssertions> NotContainLabels(params string[] labels)
            => NotContainLabels(labels, string.Empty);

        [CustomAssertion]
        public AndConstraint<CompletionResultAssertions> NotContainLabels(IEnumerable<string> labels, string because = "", params object[] reasonArgs)
            => NotContainAttributes(labels, i => i.label, "label", "labels", because, reasonArgs);

        [CustomAssertion]
        public AndConstraint<CompletionResultAssertions> NotContainInsertTexts(params string[] insertTexts)
            => NotContainInsertTexts(insertTexts, string.Empty);

        [CustomAssertion]
        public AndConstraint<CompletionResultAssertions> NotContainInsertTexts(IEnumerable<string> insertTexts, string because = "", params object[] reasonArgs)
            => NotContainAttributes(insertTexts, i => i.insertText, "insert text", "insert texts", because, reasonArgs);

        public AndConstraint<CompletionResultAssertions> NotContainAttributes(IEnumerable<string> attributes, Func<CompletionItem, string> attributeSelector, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);

            var actual = Subject.Completions?.Select(attributeSelector).ToArray() ?? Array.Empty<string>();
            var expected = attributes.ToArray();

            var errorMessage = GetAssertCollectionNotContainMessage(actual, expected, GetName(), "label", "labels");

            Execute.Assertion.ForCondition(errorMessage == null)
                .BecauseOf(because, reasonArgs)
                .FailWith(errorMessage);

            return new AndConstraint<CompletionResultAssertions>(this);
        }

        [CustomAssertion]
        private static string GetName() => CallerIdentifier.DetermineCallerIdentity() ?? "completion list items";
    }
}
