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

using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal static class AnalysisValueAssertionsExtensions {
        public static AndWhichConstraint<TAssertion, AnalysisValueTestInfo<TAnalysisValue>> OfPythonMemberType<TAssertion, TAnalysisValue>(this AndWhichConstraint<TAssertion, AnalysisValueTestInfo<TAnalysisValue>> constraint, PythonMemberType memberType) 
            where TAnalysisValue : IAnalysisValue {
            new AnalysisValueAssertions<TAnalysisValue>(constraint.Which).HaveMemberType(memberType);
            return constraint;
        }

        public static AndWhichConstraint<TAssertion, AnalysisValueTestInfo<TAnalysisValue>> WithMemberOfType<TAssertion, TAnalysisValue>(this AndWhichConstraint<TAssertion, AnalysisValueTestInfo<TAnalysisValue>> constraint, string name, PythonMemberType memberType) 
            where TAnalysisValue : IAnalysisValue {
            var member = new AnalysisValueAssertions<TAnalysisValue>(constraint.Which).HaveMember<IAnalysisValue>(name) .Which;
            new AnalysisValueAssertions<IAnalysisValue>(member).HaveMemberType(memberType);
            return constraint;
        }

        public static AndWhichConstraint<TAssertion, AnalysisValueTestInfo<IClassInfo>> 
            WithMethodResolutionOrder<TAssertion>(this AndWhichConstraint<TAssertion, AnalysisValueTestInfo<IClassInfo>> constraint, params string[] classNames) {
            constraint.Which.Should().HaveMethodResolutionOrder(classNames);
            return constraint;
        }
    }
}