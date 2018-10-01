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
using TestUtilities;
using static Microsoft.PythonTools.Analysis.FluentAssertions.AssertionsUtilities;

namespace Microsoft.PythonTools.Analysis.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal sealed class ReferenceCollectionAssertions : SelfReferencingCollectionAssertions<Reference, ReferenceCollectionAssertions> {
        public ReferenceCollectionAssertions(IEnumerable<Reference> references) : base(references) {}

        protected override string Identifier => nameof(Reference) + "Collection";

        [CustomAssertion]
        public AndConstraint<ReferenceCollectionAssertions> HaveReferenceAt(IPythonProjectEntry projectEntry, int startLine, int startCharacter, int endLine, int endCharacter, ReferenceKind? referenceKind = null, string because = "", params object[] reasonArgs) {
            var range = new Range {
                start = new Position {
                    line = startLine,
                    character = startCharacter
                },
                end = new Position {
                    line = endLine,
                    character = endCharacter
                }
            };

            var error = FindReference(projectEntry.DocumentUri, projectEntry.ModuleName, range, referenceKind);
            Execute.Assertion.ForCondition(error == string.Empty)
                .BecauseOf(because, reasonArgs)
                .FailWith(error);

            return new AndConstraint<ReferenceCollectionAssertions>(this);
        }

        [CustomAssertion]
        public AndConstraint<ReferenceCollectionAssertions> OnlyHaveReference(Uri documentUri, (int startLine, int startCharacter, int endLine, int endCharacter) range, ReferenceKind? referenceKind, string because = "", params object[] reasonArgs)
            => OnlyHaveReferences(new []{(documentUri, range, referenceKind)}, because, reasonArgs);

        [CustomAssertion]
        public AndConstraint<ReferenceCollectionAssertions> OnlyHaveReferences(params (Uri documentUri, (int startLine, int startCharacter, int endLine, int endCharacter) range, ReferenceKind? referenceKind)[] references)
            => OnlyHaveReferences(references, string.Empty);

        [CustomAssertion]
        public AndConstraint<ReferenceCollectionAssertions> OnlyHaveReferences(IEnumerable<(Uri documentUri, (int startLine, int startCharacter, int endLine, int endCharacter) range, ReferenceKind? referenceKind)> references, string because = "", params object[] reasonArgs) {
            var expected = references.ToArray();
            foreach (var (documentUri, (startLine, startCharacter, endLine, endCharacter), referenceKind) in expected) {
                HaveReferenceAt(documentUri, startLine, startCharacter, endLine, endCharacter, referenceKind, because, reasonArgs);
            }

            var excess = Subject.Select(r => (r.uri, (r.range.start.line, r.range.start.character, r.range.end.line, r.range.end.character), r._kind))
                .Except(expected)
                .ToArray();

            if (excess.Length > 0) {
                var excessString = string.Join(", ", excess.Select(Format));
                var errorMessage = expected.Length > 1
                    ? $"Expected {GetSubjectName()} to have only {expected.Length} references{{reason}}, but it also has references: {excessString}."
                    : expected.Length > 0
                        ? $"Expected {GetSubjectName()} to have only one reference{{reason}}, but it also has references: {excessString}."
                        : $"Expected {GetSubjectName()} to have no references{{reason}}, but it has references: {excessString}.";

                Execute.Assertion.BecauseOf(because, reasonArgs).FailWith(errorMessage);
            }

            return new AndConstraint<ReferenceCollectionAssertions>(this);
        }

        private static string Format((Uri uri, (int, int, int, int) range, ReferenceKind? kind) reference) 
            => $"({TestData.GetTestRelativePath(reference.uri)}, {reference.range.ToString()}, {reference.kind})";

        [CustomAssertion]
        public AndConstraint<ReferenceCollectionAssertions> HaveReferenceAt(Uri documentUri, int startLine, int startCharacter, int endLine, int endCharacter, ReferenceKind? referenceKind = null, string because = "", params object[] reasonArgs) {
            var range = new Range {
                start = new Position {
                    line = startLine,
                    character = startCharacter
                },
                end = new Position {
                    line = endLine,
                    character = endCharacter
                }
            };
            
            var error = FindReference(documentUri, TestData.GetTestRelativePath(documentUri), range, referenceKind);
            Execute.Assertion.ForCondition(error == string.Empty)
                .BecauseOf(because, reasonArgs)
                .FailWith(error);

            return new AndConstraint<ReferenceCollectionAssertions>(this);
        }

        [CustomAssertion]
        private string FindReference(Uri documentUri, string moduleName, Range range, ReferenceKind? referenceKind = null) {
            var candidates = Subject.Where(av => Equals(av.uri, documentUri)).ToArray();
            if (candidates.Length == 0) {
                return $"Expected {GetSubjectName()} to have reference in the module '{moduleName}'{{reason}}, but no references has been found.";
            }

            foreach (var candidate in candidates.Where(c => RangeEquals(c.range, range))) {
                return referenceKind.HasValue && candidate._kind != referenceKind
                    ? $"Expected {GetSubjectName()} to have reference of type '{referenceKind}'{{reason}}, but reference in module '{moduleName}' at {range.ToString()} has type '{candidate._kind}'"
                    : string.Empty;
            }

            var errorMessage = $"Expected {GetSubjectName()} to have reference at {range.ToString()}{{reason}}, but module '{moduleName}' has no references at that range.";
            if (!referenceKind.HasValue) {
                return errorMessage;
            }

            var matchingTypes = candidates.Where(av => av._kind == referenceKind).ToArray();
            var matchingTypesString = matchingTypes.Length > 0
                ? $"References that match type '{referenceKind}' have spans {string.Join(" ,", matchingTypes.Select(av => av.range.ToString()))}"
                : $"There are no references with type '{referenceKind}' either";

            return $"{errorMessage} {matchingTypesString}";
        }
        
        [CustomAssertion]
        private static string GetSubjectName() => CallerIdentifier.DetermineCallerIdentity() ?? "collection";
    }
}