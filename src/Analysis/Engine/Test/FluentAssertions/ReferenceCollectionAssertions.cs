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

namespace Microsoft.PythonTools.Analysis.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal sealed class ReferenceCollectionAssertions : SelfReferencingCollectionAssertions<Reference, ReferenceCollectionAssertions> {
        public ReferenceCollectionAssertions(IEnumerable<Reference> references) : base(references) { }

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
            
            var error = FindReference(documentUri, documentUri.AbsolutePath, range, referenceKind);
            Execute.Assertion.ForCondition(error == string.Empty)
                .BecauseOf(because, reasonArgs)
                .FailWith(error);

            return new AndConstraint<ReferenceCollectionAssertions>(this);
        }

        [CustomAssertion]
        private string FindReference(Uri documentUri, string moduleName, Range range, ReferenceKind? referenceKind = null) {
            var candidates = Subject.Where(av => Equals(av.uri, documentUri)).ToArray();
            if (candidates.Length == 0) {
                return $"Expected {GetName()} to have reference in the module '{moduleName}'{{reason}}, but no references has been found.";
            }

            foreach (var candidate in candidates) {
                if (RangeEquals(candidate.range, range)) {
                    return !referenceKind.HasValue || candidate._kind == referenceKind
                        ? string.Empty
                        : $"Expected {GetName()} to have reference of type '{referenceKind}'{{reason}}, but reference in module '{moduleName}' at {ToString(range)} has type '{candidate._kind}'";
                }
            }

            var errorMessage = $"Expected {GetName()} to have reference at {ToString(range)}{{reason}}, but module '{moduleName}' has no references at that range.";
            if (!referenceKind.HasValue) {
                return errorMessage;
            }

            var matchingTypes = candidates.Where(av => av._kind == referenceKind).ToArray();
            var matchingTypesString = matchingTypes.Length > 0
                ? $"References that match type '{referenceKind}' have spans {string.Join(" ,", matchingTypes.Select(av => ToString(av.range)))}"
                : $"There are no references with type '{referenceKind}' either";

            return $"{errorMessage} {matchingTypesString}";
        }

        private static string ToString(Range range)
            => $"({range.start.line}, {range.start.character}) - ({range.end.line}, {range.end.character})";

        private static bool RangeEquals(Range r1, Range r2)
            => r1.start.line == r2.start.line
               && r1.start.character == r2.start.character
               && r1.end.line == r2.end.line
               && r1.end.character == r2.end.character;

        [CustomAssertion]
        private static string GetName() => CallerIdentifier.DetermineCallerIdentity() ?? "collection";
    }
}