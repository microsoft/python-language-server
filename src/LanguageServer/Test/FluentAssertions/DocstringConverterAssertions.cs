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

using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using FluentAssertions.Primitives;
using Microsoft.Python.Core;
using Microsoft.Python.LanguageServer.Documentation;

namespace Microsoft.Python.LanguageServer.Tests.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal static class DocstringAssertions {
        public static AndConstraint<StringAssertions> ConvertToPlaintext(this StringAssertions a, string plaintext, string because = "", params object[] reasonArgs) {
            DocstringConverter.ToPlaintext(a.Subject.NormalizeLineEndings()).Should().Be(plaintext.NormalizeLineEndings().TrimEnd(), because, reasonArgs);
            return new AndConstraint<StringAssertions>(a);
        }

        public static AndConstraint<StringAssertions> ConvertToMarkdown(this StringAssertions a, string markdown, string because = "", params object[] reasonArgs) {
            DocstringConverter.ToMarkdown(a.Subject.NormalizeLineEndings()).Should().Be(markdown.NormalizeLineEndings().TrimEnd(), because, reasonArgs);
            return new AndConstraint<StringAssertions>(a);
        }
    }
}
