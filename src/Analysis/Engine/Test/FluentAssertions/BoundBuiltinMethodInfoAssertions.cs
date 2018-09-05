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

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Analysis.Values;

namespace Microsoft.PythonTools.Analysis.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal sealed class BoundBuiltinMethodInfoAssertions : AnalysisValueAssertions<BoundBuiltinMethodInfo, BoundBuiltinMethodInfoAssertions> {
        public BoundBuiltinMethodInfoAssertions(AnalysisValueTestInfo<BoundBuiltinMethodInfo> subject) : base(subject) {}

        protected override string Identifier => nameof(BoundBuiltinMethodInfo);

        public AndWhichConstraint<BoundBuiltinMethodInfoAssertions, OverloadResultTestInfo> HaveOverloadWithParametersAt(int index, string because = "", params object[] reasonArgs) {
            var constraint = HaveOverloadAt(index);
            var overload = constraint.Which;
            var function = Subject.Method.Function;

            Execute.Assertion.ForCondition(overload.Value.Parameters.Length > 0)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected overload {overload.Name} at index {index} of {function.DeclaringModule.Name}.{function.Name} to have parameters{{reason}}, but it has none.");

            return constraint;
        }
    }
}