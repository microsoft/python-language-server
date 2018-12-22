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

using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using static Microsoft.Python.Analysis.Tests.FluentAssertions.AssertionsUtilities;

namespace Microsoft.Python.Analysis.Tests.FluentAssertions {
    internal sealed class VariableCollectionAssertions : ReferenceTypeAssertions<IVariableCollection, VariableCollectionAssertions> {
        public VariableCollectionAssertions(IVariableCollection collection) {
            Subject = collection;
        }

        protected override string Identifier => nameof(IVariableCollection);

        public AndConstraint<VariableCollectionAssertions> Contain(params object[] expected) {
            var actual = Subject.Select(v => v.Name).ToArray();
            var errorMessage = GetAssertCollectionContainsMessage(actual, expected, "collection", "item", "items");

            Execute.Assertion.ForCondition(errorMessage == null)
                .BecauseOf(string.Empty, string.Empty)
                .FailWith(errorMessage);

            return new AndConstraint<VariableCollectionAssertions>(this);
        }


        public static string GetAssertMessage<T>(IEnumerable<T> actual, IEnumerable<T> expected, string name) where T : class
            => !actual.SetEquals(expected) ? $"Expected collection to contain '{expected}'{{reason}}, but it has {actual}." : null;
    }
}
