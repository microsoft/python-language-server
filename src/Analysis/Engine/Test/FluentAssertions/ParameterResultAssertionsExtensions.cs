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

using FluentAssertions;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.PythonTools.Analysis.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal static class ParameterResultAssertionsExtensions {
        public static AndWhichConstraint<TAssertions, ParameterResult> WithName<TAssertions>(this AndWhichConstraint<TAssertions, ParameterResult> constraint, string name, string because = "", params object[] reasonArgs) {
            constraint.Which.Should().HaveName(name, because, reasonArgs);
            return constraint;
        }

        public static AndWhichConstraint<TAssertions, ParameterResult> WithType<TAssertions>(this AndWhichConstraint<TAssertions, ParameterResult> constraint, string type, string because = "", params object[] reasonArgs) {
            constraint.Which.Should().HaveType(type, because, reasonArgs);
            return constraint;
        }

        public static AndWhichConstraint<TAssertions, ParameterResult> WithNoDefaultValue<TAssertions>(this AndWhichConstraint<TAssertions, ParameterResult> constraint, string because = "", params object[] reasonArgs) {
            constraint.Which.Should().HaveNoDefaultValue(because, reasonArgs);
            return constraint;
        }
    }
}