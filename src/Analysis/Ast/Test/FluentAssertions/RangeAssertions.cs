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

using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Python.Core.Text;
using static Microsoft.Python.Analysis.Tests.FluentAssertions.AssertionsUtilities;

namespace Microsoft.Python.Analysis.Tests.FluentAssertions {
    internal sealed class RangeAssertions {
        public Range? Subject { get; }

        public RangeAssertions(Range? range) {
            Subject = range;
        }

        public AndConstraint<RangeAssertions> Be(int startLine, int startCharacter, int endLine, int endCharacter, string because = "", params object[] becauseArgs) {
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

            return Be(range, because, becauseArgs);
        }

        public AndConstraint<RangeAssertions> Be(Range range, string because = "", params object[] becauseArgs) {
            Execute.Assertion.ForCondition(Subject.HasValue && RangeEquals(Subject.Value, range))
                .BecauseOf(because, becauseArgs)
                .FailWith($"Expected range to be {range.ToString()}{{reason}}, but found {SubjectString}.");

            return new AndConstraint<RangeAssertions>(this);
        }

        private string SubjectString => Subject.HasValue ? Subject.Value.ToString() : "none";
    }
}
