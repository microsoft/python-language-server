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
using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis.Tests.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal static class VariableAssertionsExtensions {
        public static AndWhichConstraint<TAssertion, VariableTestInfo> OfType<TAssertion>(
            this AndWhichConstraint<TAssertion, VariableTestInfo> andWhichConstraint,
            BuiltinTypeId typeId, string because = "", params object[] reasonArgs) {
            andWhichConstraint.Which.Should().HaveType(typeId, because, reasonArgs);
            return andWhichConstraint;
        }

        public static AndWhichConstraint<TAssertion, VariableTestInfo> OfTypes<TAssertion>(
            this AndWhichConstraint<TAssertion, VariableTestInfo> andWhichConstraint, params BuiltinTypeId[] typeIds)
            => andWhichConstraint.OfTypes(typeIds, string.Empty);

        public static AndWhichConstraint<TAssertion, VariableTestInfo> OfTypes<TAssertion>(
            this AndWhichConstraint<TAssertion, VariableTestInfo> andWhichConstraint, IEnumerable<BuiltinTypeId> typeIds, string because = "", params object[] reasonArgs) {
            andWhichConstraint.Which.Should().HaveTypes(typeIds, because, reasonArgs);
            return andWhichConstraint;
        }

        public static AndWhichConstraint<TAssertion, VariableTestInfo> WithNoTypes<TAssertion>(
            this AndWhichConstraint<TAssertion, VariableTestInfo> andWhichConstraint, string because = "", params object[] reasonArgs) {
            andWhichConstraint.Which.Should().HaveNoTypes(because, reasonArgs);
            return andWhichConstraint;
        }

        public static AndWhichConstraint<TAssertion, VariableTestInfo> OfTypes<TAssertion>(
            this AndWhichConstraint<TAssertion, VariableTestInfo> andWhichConstraint, params string[] classNames)
            => andWhichConstraint.OfTypes(classNames, string.Empty);

        public static AndWhichConstraint<TAssertion, VariableTestInfo> OfTypes<TAssertion>(
            this AndWhichConstraint<TAssertion, VariableTestInfo> andWhichConstraint, IEnumerable<string> classNames, string because = "", params object[] reasonArgs) {
            andWhichConstraint.Which.Should().HaveClassNames(classNames, because, reasonArgs);
            return andWhichConstraint;
        }
    }
}
