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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Collections;
using FluentAssertions.Execution;
using Microsoft.Python.LanguageServer;
using static Microsoft.PythonTools.Analysis.FluentAssertions.AssertionsUtilities;

namespace Microsoft.PythonTools.Analysis.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal sealed class TextEditCollectionAssertions : SelfReferencingCollectionAssertions<TextEdit, TextEditCollectionAssertions> {
        public TextEditCollectionAssertions(IEnumerable<TextEdit> references) : base(references) {}

        protected override string Identifier => nameof(TextEdit) + "Collection";

        
        [CustomAssertion]
        public AndConstraint<TextEditCollectionAssertions> OnlyHaveTextEdit(string expectedText, (int startLine, int startCharacter, int endLine, int endCharacter) expectedRange, string because = "", params object[] reasonArgs)
            => OnlyHaveTextEdits(new []{(expectedText, expectedRange)}, because, reasonArgs);

        [CustomAssertion]
        public AndConstraint<TextEditCollectionAssertions> OnlyHaveTextEdits(params (string expectedText, (int startLine, int startCharacter, int endLine, int endCharacter) expectedRange)[] textEdits)
            => OnlyHaveTextEdits(textEdits, string.Empty);

        [CustomAssertion]
        public AndConstraint<TextEditCollectionAssertions> OnlyHaveTextEdits(IEnumerable<(string expectedText, (int startLine, int startCharacter, int endLine, int endCharacter) expectedRange)> textEdits, string because = "", params object[] reasonArgs) {
            var expected = textEdits.ToArray();
            foreach (var (expectedText, (startLine, startCharacter, endLine, endCharacter)) in expected) {
                HaveTextEditAt(expectedText, (startLine, startCharacter, endLine, endCharacter), because, reasonArgs);
            }

            var excess = Subject.Select(r => (r.newText, (r.range.start.line, r.range.start.character, r.range.end.line, r.range.end.character)))
                .Except(expected)
                .ToArray();

            if (excess.Length > 0) {
                var excessString = string.Join(", ", excess.Select(((string text, (int, int, int, int) range) te) => $"({te.text}, {GetName(te.range)})"));
                var errorMessage = expected.Length > 1
                    ? $"Expected {GetSubjectName()} to have only {expected.Length} textEdits{{reason}}, but it also has textEdits: {excessString}."
                    : expected.Length > 0
                        ? $"Expected {GetSubjectName()} to have only one reference{{reason}}, but it also has textEdits: {excessString}."
                        : $"Expected {GetSubjectName()} to have no textEdits{{reason}}, but it has textEdits: {excessString}.";

                Execute.Assertion.BecauseOf(because, reasonArgs).FailWith(errorMessage);
            }

            return new AndConstraint<TextEditCollectionAssertions>(this);
        }
        
        [CustomAssertion]
        public AndConstraint<TextEditCollectionAssertions> HaveTextEditAt(string expectedText, (int startLine, int startCharacter, int endLine, int endCharacter) expectedRange, string because = "", params object[] reasonArgs) {
            var range = new Range {
                start = new Position { line = expectedRange.startLine, character = expectedRange.startCharacter },
                end = new Position { line = expectedRange.endLine, character = expectedRange.endCharacter }
            };
            
            var errorMessage = GetHaveTextEditErrorMessage(expectedText, range);
            Execute.Assertion.ForCondition(errorMessage == string.Empty)
                .BecauseOf(because, reasonArgs)
                .FailWith(errorMessage);

            return new AndConstraint<TextEditCollectionAssertions>(this);
        }

        [CustomAssertion]
        private string GetHaveTextEditErrorMessage(string expectedText, Range expectedRange) {
            var candidates = Subject.Where(av => string.Equals(av.newText, expectedText, StringComparison.Ordinal)).ToArray();
            if (candidates.Length == 0) {
                return $"Expected {GetSubjectName()} to have text edit with newText '{expectedText}'{{reason}}, but "
                    + (Subject.Any() ? "it is empty" : $"it has {GetQuotedNames(Subject.Select(te => te.newText))}");
            }

            var candidatesWithRange = candidates.Where(c => RangeEquals(c.range, expectedRange)).ToArray();
            if (candidatesWithRange.Length > 1) {
                return $"Expected {GetSubjectName()} to have only one text edit with newText '{expectedText}' and range {GetName(expectedRange)}{{reason}}, but there are {candidatesWithRange.Length}";
            }

            if (candidatesWithRange.Length == 0) {
                return $"Expected {GetSubjectName()} to have text edit with newText '{expectedText}' in range {GetName(expectedRange)} {{reason}}, but "
                    + (candidatesWithRange.Length == 1 
                        ? $"it has range {GetName(candidatesWithRange[0].range)}" 
                        : $"they are in ranges {string.Join(", ", candidatesWithRange.Select(te => GetName(te.range)))}");
            }

            return string.Empty;
        }

        [CustomAssertion]
        private static string GetSubjectName() => CallerIdentifier.DetermineCallerIdentity() ?? "collection";
    }
}