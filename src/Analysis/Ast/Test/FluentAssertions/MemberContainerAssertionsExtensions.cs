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
using FluentAssertions.Execution;

namespace Microsoft.Python.Analysis.Tests.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal static class MemberContainerAssertionsExtensions {
        public static AndWhichConstraint<TAssertions, TMember> OfMemberType<TMember, TAssertions> (this AndWhichConstraint<TAssertions, TMember> constraint, PythonMemberType memberType, string because = "", params object[] reasonArgs)
            where TMember : IPythonType {

            Execute.Assertion.ForCondition(constraint.Which.MemberType == memberType)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {AssertionsUtilities.GetQuotedName(constraint.Which)} to have type '{memberType}', but found '{constraint.Which.MemberType}'");

            return new AndWhichConstraint<TAssertions, TMember>(constraint.And, constraint.Which);
        }
    }
}
