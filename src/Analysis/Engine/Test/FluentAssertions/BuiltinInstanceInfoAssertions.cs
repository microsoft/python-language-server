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
using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.PythonTools.Analysis.Values;

namespace Microsoft.PythonTools.Analysis.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal sealed class BuiltinInstanceInfoAssertions : AnalysisValueAssertions<BuiltinInstanceInfo, BuiltinInstanceInfoAssertions> {
        public BuiltinInstanceInfoAssertions(AnalysisValueTestInfo<BuiltinInstanceInfo> subject) : base(subject) {}

        protected override string Identifier => nameof(BuiltinInstanceInfo);

        public AndConstraint<BuiltinInstanceInfoAssertions> HaveClassName(string className, string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(string.Equals(Subject.ClassInfo.Name, className, StringComparison.Ordinal))
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetName()} to have class name '{className}'{{reason}}");

            return new AndConstraint<BuiltinInstanceInfoAssertions>(this);
        }
    }
}