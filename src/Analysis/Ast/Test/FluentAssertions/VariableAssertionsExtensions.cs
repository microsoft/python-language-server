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

using System;
using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Tests.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal static class VariableAssertionsExtensions {
        public static AndWhichConstraint<TAssertion, IVariable> OfType<TAssertion>(
            this AndWhichConstraint<TAssertion, IVariable> andWhichConstraint, BuiltinTypeId typeId, string because = "", params object[] reasonArgs) {
            andWhichConstraint.Subject.Value.Should().HaveType(typeId, because, reasonArgs);
            return andWhichConstraint;
        }

        public static AndWhichConstraint<TAssertion, IVariable> OfType<TAssertion>(
            this AndWhichConstraint<TAssertion, IVariable> andWhichConstraint, string typeName, string because = "", params object[] reasonArgs) {
            andWhichConstraint.Subject.Value.Should().HaveType(typeName, because, reasonArgs);
            return andWhichConstraint;
        }

        public static AndWhichConstraint<TAssertion, IVariable> OfType<TAssertion>(
            this AndWhichConstraint<TAssertion, IVariable> andWhichConstraint, Type type,  string because = "", params object[] reasonArgs) {
            andWhichConstraint.Subject.Value.Should().HaveType(type, because, reasonArgs);
            return andWhichConstraint;
        }

        public static AndWhichConstraint<TAssertion, IVariable> WithNoTypes<TAssertion>(
            this AndWhichConstraint<TAssertion, IVariable> andWhichConstraint, string because = "", params object[] reasonArgs) {
            andWhichConstraint.Subject.Value.Should().HaveNoType(because, reasonArgs);
            return andWhichConstraint;
        }
    }
}
