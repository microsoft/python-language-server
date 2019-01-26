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

namespace Microsoft.Python.LanguageServer.Tests.FluentAssertions {
    internal sealed class SourceSpanAssertions {
        public SourceSpan? Subject { get; }

        public SourceSpanAssertions(SourceSpan? span) {
            Subject = span;
        }

        public AndConstraint<SourceSpanAssertions> Be(int startLine, int startCharacter, int endLine, int endCharacter, string because = "", params object[] becauseArgs) {
            var span = new SourceSpan(
                new SourceLocation(startLine, startCharacter),
                new SourceLocation(endLine, endCharacter)
            );
            return Be(span, because, becauseArgs);
        }

        public AndConstraint<SourceSpanAssertions> Be(SourceSpan span, string because = "", params object[] becauseArgs) {
            Execute.Assertion.ForCondition(Subject.HasValue && RangeEquals(Subject.Value, span))
                .BecauseOf(because, becauseArgs)
                .FailWith($"Expected range to be {span.ToString()}{{reason}}, but found {SubjectString}.");

            return new AndConstraint<SourceSpanAssertions>(this);
        }

        private string SubjectString => Subject.HasValue ? Subject.Value.ToString() : "none";
    }
}
