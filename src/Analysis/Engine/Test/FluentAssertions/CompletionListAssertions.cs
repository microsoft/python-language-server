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
using Microsoft.Python.LanguageServer;
using static Microsoft.PythonTools.Analysis.FluentAssertions.AssertionsUtilities;

namespace Microsoft.PythonTools.Analysis.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal sealed class CompletionListAssertions : ReferenceTypeAssertions<CompletionList, CompletionListAssertions> {
        public CompletionListAssertions(CompletionList subject) {
            Subject = subject;
        }

        protected override string Identifier => nameof(CompletionList);

        public AndConstraint<CompletionListAssertions> OnlyHaveLabels(params string[] labels)
            => OnlyHaveLabels(labels, string.Empty);

        public AndConstraint<CompletionListAssertions> OnlyHaveLabels(IEnumerable<string> labels, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);

            var actual = Subject.items?.Select(i => i.label).ToArray() ?? new string[0];
            var expected = labels.ToArray();

            var errorMessage = GetAssertCollectionOnlyContainsMessage(actual, expected, GetName(), "label", "labels");

            Execute.Assertion.ForCondition(errorMessage == null)
                .BecauseOf(because, reasonArgs)
                .FailWith(errorMessage);

            return new AndConstraint<CompletionListAssertions>(this);
        }

        [CustomAssertion]
        public AndWhichConstraint<CompletionListAssertions, CompletionItem> HaveItem(string label, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);

            var actual = Subject?.items.Where(i => string.Equals(i.label, label, StringComparison.Ordinal)).ToArray() ?? Array.Empty<CompletionItem>();
            var errorMessage = GetAssertCollectionContainsMessage(actual.Select(i => i.label).ToArray(), new [] { label }, GetName(), "label", "labels");

            Execute.Assertion.ForCondition(errorMessage == null)
                .BecauseOf(because, reasonArgs)
                .FailWith(errorMessage);

            return new AndWhichConstraint<CompletionListAssertions, CompletionItem>(this, actual[0]);
        }

        [CustomAssertion]
        public AndConstraint<CompletionListAssertions> HaveLabels(params string[] labels)
            => HaveLabels(labels, string.Empty);

        [CustomAssertion]
        public AndConstraint<CompletionListAssertions> HaveLabels(IEnumerable<string> labels, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);

            var actual = Subject.items?.Select(i => i.label).ToArray() ?? new string[0];
            var expected = labels.ToArray();

            var errorMessage = GetAssertCollectionContainsMessage(actual, expected, GetName(), "label", "labels");

            Execute.Assertion.ForCondition(errorMessage == null)
                .BecauseOf(because, reasonArgs)
                .FailWith(errorMessage);

            return new AndConstraint<CompletionListAssertions>(this);
        }

        [CustomAssertion]
        public AndConstraint<CompletionListAssertions> NotContainLabels(params string[] labels)
            => NotContainLabels(labels, string.Empty);

        [CustomAssertion]
        public AndConstraint<CompletionListAssertions> NotContainLabels(IEnumerable<string> labels, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);

            var actual = Subject.items?.Select(i => i.label).ToArray() ?? new string[0];
            var expected = labels.ToArray();

            var errorMessage = GetAssertCollectionNotContainMessage(actual, expected, GetName(), "label", "labels");

            Execute.Assertion.ForCondition(errorMessage == null)
                .BecauseOf(because, reasonArgs)
                .FailWith(errorMessage);

            return new AndConstraint<CompletionListAssertions>(this);
        }

        [CustomAssertion]
        private static string GetName() => CallerIdentifier.DetermineCallerIdentity() ?? "completion list items";
    }
}