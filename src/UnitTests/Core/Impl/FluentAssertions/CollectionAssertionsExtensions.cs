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
using System.Linq;
using FluentAssertions;
using FluentAssertions.Collections;

namespace Microsoft.Python.Tests.Utilities.FluentAssertions {
    public static class CollectionAssertionsExtensions {
        public static AndConstraint<GenericCollectionAssertions<TAssertions>> OnlyContain<TAssertions>(
            this GenericCollectionAssertions<TAssertions> assertions, params TAssertions[] expected) 
            => assertions.HaveCount(expected.Length).And.Contain(expected);

        public static AndConstraint<GenericCollectionAssertions<TAssertions>> OnlyContain<TAssertions>(
            this GenericCollectionAssertions<TAssertions> assertions, IReadOnlyCollection<TAssertions> expected, string because = "", params object[] reasonArgs) 
            => assertions.HaveCount(expected.Count, because, reasonArgs).And.Contain(expected, because, reasonArgs);

        public static AndConstraint<StringCollectionAssertions> OnlyContain(
            this StringCollectionAssertions assertions, params string[] expected) 
            => assertions.HaveCount(expected.Length).And.Contain(expected);

        public static AndConstraint<StringCollectionAssertions> OnlyContain(
            this StringCollectionAssertions assertions, IReadOnlyCollection<string> expected, string because = "", params object[] reasonArgs) 
            => assertions.HaveCount(expected.Count, because, reasonArgs).And.Contain(expected, because, reasonArgs);

        public static AndConstraint<GenericCollectionAssertions<TAssertions>> BeEquivalentToWithStrictOrdering<TAssertions>(
            this GenericCollectionAssertions<TAssertions> assertions, IEnumerable<TAssertions> expected, string because = "", params object[] reasonArgs)
            => assertions.BeEquivalentTo(expected, options => options.WithStrictOrdering(), because, reasonArgs);
    }
}
