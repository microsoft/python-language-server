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

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using Microsoft.Python.LanguageServer;
using Microsoft.PythonTools.Analysis.Documentation;

namespace Microsoft.PythonTools.Analysis.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal sealed class SignatureInformationAssertions : ReferenceTypeAssertions<SignatureInformation, SignatureInformationAssertions> {
        public SignatureInformationAssertions(SignatureInformation subject) {
            Subject = subject;
        }

        protected override string Identifier => nameof(SignatureInformation);

        public AndConstraint<SignatureInformationAssertions> HaveNoParameters(string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);

            var count = Subject.parameters?.Length ?? 0;
            Execute.Assertion.ForCondition(count == 0)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected signature '{Subject.label}' to have no parameters{{reason}}, but it has {count} instead.");

            return new AndConstraint<SignatureInformationAssertions>(this);
        }

        public AndConstraint<SignatureInformationAssertions> OnlyHaveParameterLabels(params string[] labels)
            => OnlyHaveParameterLabels(labels, string.Empty);

        public AndConstraint<SignatureInformationAssertions> OnlyHaveParameterLabels(IEnumerable<string> labels, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);

            var actual = Subject.parameters?.Select(i => i.label).ToArray() ?? new string[0];
            var expected = labels.ToArray();

            var errorMessage = AssertionsUtilities.GetAssertCollectionOnlyContainsMessage(actual, expected, $"signature '{Subject.label}'", "parameter label", "parameter labels");

            Execute.Assertion.ForCondition(errorMessage == null)
                .BecauseOf(because, reasonArgs)
                .FailWith(errorMessage);

            return new AndConstraint<SignatureInformationAssertions>(this);
        }
        
        public AndConstraint<SignatureInformationAssertions> HaveMarkdownDocumentation(string documentation, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);

            var errorMessage = Subject.documentation == null
                ? $"Expected signature '{Subject.label}' to have markdown documentation {documentation}{{reason}}, but it has no documentation"
                : Subject.documentation.kind != MarkupKind.Markdown
                    ? $"Expected signature '{Subject.label}' to have markdown documentation '{documentation}'{{reason}}, but it has {Subject.documentation.kind} documentation"
                    : !string.Equals(Subject.documentation.value, documentation)
                        ? $"Expected signature '{Subject.label}' to have markdown documentation '{documentation}'{{reason}}, but it has '{Subject.documentation.value}'"
                        : null;

            Execute.Assertion.ForCondition(errorMessage == null)
                .BecauseOf(because, reasonArgs)
                .FailWith(errorMessage);

            return new AndConstraint<SignatureInformationAssertions>(this);
        }
    }
}