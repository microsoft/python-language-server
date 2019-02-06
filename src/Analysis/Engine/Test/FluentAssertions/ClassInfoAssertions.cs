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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.PythonTools.Analysis.Values;
using static Microsoft.PythonTools.Analysis.FluentAssertions.AssertionsUtilities;

namespace Microsoft.PythonTools.Analysis.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal sealed class ClassInfoAssertions : AnalysisValueAssertions<IClassInfo, ClassInfoAssertions> {
        public ClassInfoAssertions(AnalysisValueTestInfo<IClassInfo> subject) : base(subject) {}

        protected override string Identifier => nameof(IClassInfo);
        
        public AndWhichConstraint<ClassInfoAssertions, IClassScope> HaveScope(string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(Subject.Scope != null)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetName()} to have scope specified{{reason}}.");

            return new AndWhichConstraint<ClassInfoAssertions, IClassScope>(this, Subject.Scope);
        }

        public AndConstraint<ClassInfoAssertions> HaveMethodResolutionOrder(params string[] classNames)
            => HaveMethodResolutionOrder(classNames, string.Empty);

        public AndConstraint<ClassInfoAssertions> HaveMethodResolutionOrder(IEnumerable<string> classNames, string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(Subject.Mro != null && Subject.Mro.IsValid)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetName()} to have valid method resolution order.");

            var expected = classNames.ToArray();
            var actual = FlattenAnalysisValues(Subject.Mro.SelectMany(av => av))
                .Select(av => av.ShortDescription)
                .ToArray();

            var errorMessage = GetAssertSequenceEqualMessage(actual, expected, GetName(), "MRO");
            Execute.Assertion.ForCondition(errorMessage == null)
                .BecauseOf(because, reasonArgs)
                .FailWith(errorMessage);

            return new AndConstraint<ClassInfoAssertions>(this);
        }

        public AndConstraint<ClassInfoAssertions> HaveInvalidMethodResolutionOrder(string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(Subject.Mro == null || !Subject.Mro.IsValid)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetName()} to have invalid method resolution order.");

            return new AndConstraint<ClassInfoAssertions>(this);
        }

        protected override string GetName() => $"class {GetQuotedName(Subject)}";
    }
}
