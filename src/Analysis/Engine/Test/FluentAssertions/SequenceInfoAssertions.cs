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
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal sealed class SequenceInfoAssertions : AnalysisValueAssertions<SequenceInfo, SequenceInfoAssertions> {
        public SequenceInfoAssertions(AnalysisValueTestInfo<SequenceInfo> subject) : base(subject) {}

        protected override string Identifier => nameof(SequenceInfo);

        public AndConstraint<SequenceInfoAssertions> HaveIndexType(int index, BuiltinTypeId type, string because = "", params object[] reasonArgs) 
            => HaveIndexTypes(index, new[] {type}, because, reasonArgs);

        public AndConstraint<SequenceInfoAssertions> HaveIndexTypes(int index, params BuiltinTypeId[] types) 
            => HaveIndexTypes(index, types, string.Empty);

        public AndConstraint<SequenceInfoAssertions> HaveIndexTypes(int index, IEnumerable<BuiltinTypeId> types, string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(Subject.IndexTypes.Length > index)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetName()} to have name index type at {index}{{reason}}, but it {GetIndexTypesCountString(Subject.IndexTypes.Length)}.");

            var testInfo = new VariableDefTestInfo(Subject.IndexTypes[index], $"{GetName()}[{0}]", OwnerScope);
            testInfo.Should().HaveTypes(types, because, reasonArgs);

            return new AndConstraint<SequenceInfoAssertions>(this);
        }

        private static string GetIndexTypesCountString(int count)
            => count > 1
                ? $"only has {count} index types"
                : count > 0
                    ? "only has one overload"
                    : "has no index types";
    }
}