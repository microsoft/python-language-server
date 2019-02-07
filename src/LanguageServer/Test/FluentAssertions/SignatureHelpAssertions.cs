﻿// Copyright(c) Microsoft Corporation
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
using Microsoft.Python.LanguageServer.Protocol;
using static Microsoft.Python.Analysis.Tests.FluentAssertions.AssertionsUtilities;

namespace Microsoft.Python.LanguageServer.Tests.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal sealed class SignatureHelpAssertions : ReferenceTypeAssertions<SignatureHelp, SignatureHelpAssertions> {
        public SignatureHelpAssertions(SignatureHelp subject) {
            Subject = subject;
        }

        protected override string Identifier => nameof(SignatureHelp);

        public AndWhichConstraint<SignatureHelpAssertions, SignatureInformation> OnlyHaveSignature(string signature, string because = "", params object[] reasonArgs) {
            var constraint = HaveSingleSignature();
            var actual = constraint.Which.label;

            Execute.Assertion.ForCondition(string.Equals(actual, signature, StringComparison.Ordinal))
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected SignatureHelp to have single signature '{signature}'{{reason}}, but it has '{actual}'.");

            return constraint;
        }

        public AndConstraint<SignatureHelpAssertions> OnlyHaveSignatures(string[] signatures)
            => OnlyHaveSignatures(signatures, string.Empty);

        public AndConstraint<SignatureHelpAssertions> OnlyHaveSignatures(IEnumerable<string> signatures, string because = "", params object[] reasonArgs) {
            var expected = signatures.ToArray();
            var actual = Subject.signatures.Select(s => s.label).ToArray();
            var errorMessage = GetAssertCollectionOnlyContainsMessage(actual, expected, "SignatureHelp", "signature", "signatures");

            Execute.Assertion.ForCondition(errorMessage == null)
                .BecauseOf(because, reasonArgs)
                .FailWith(errorMessage);

            return new AndConstraint<SignatureHelpAssertions>(this);
        }

        public AndWhichConstraint<SignatureHelpAssertions, SignatureInformation> HaveSingleSignature(string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);

            var signature = Subject.signatures != null && Subject.signatures.Length > 0 ? Subject.signatures[0] : null;

            Execute.Assertion.ForCondition(signature != null)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected SignatureHelp to have single signature{{reason}}, but it has none.");

            Execute.Assertion.ForCondition(Subject.signatures.Length == 1)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected SignatureHelp to have single signature{{reason}}, but it has {Subject.signatures.Length}.");

            return new AndWhichConstraint<SignatureHelpAssertions, SignatureInformation>(this, signature);
        }
    }
}
