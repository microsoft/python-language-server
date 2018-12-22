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

using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using static Microsoft.Python.Analysis.Tests.FluentAssertions.AssertionsUtilities;

namespace Microsoft.Python.Analysis.Tests.FluentAssertions {
    internal sealed class MemberTestInfo {
        private readonly IPythonType _member;
        private readonly IScope _scope;
        public string Name { get; }

        public MemberTestInfo(IPythonType member, string name, IScope scope) {
            _member = member;
            Name = name;
            _scope = scope;
        }

        public PythonTypeAssertions Should() => new PythonTypeAssertions(_member, Name, _scope);
    }

    internal sealed class PythonTypeAssertions : ReferenceTypeAssertions<IPythonType, PythonTypeAssertions> {
        private readonly string _moduleName;
        private readonly string _name;
        private readonly IScope _scope;

        public PythonTypeAssertions(IPythonType member, string name, IScope scope) {
            Subject = member;
            _name = name;
            _scope = scope;
            _moduleName = scope.Name;
        }

        protected override string Identifier => nameof(IPythonType);

        public AndConstraint<PythonTypeAssertions> HaveType(BuiltinTypeId typeId, string because = "", params object[] reasonArgs) {
            var languageVersionIs3X = Is3X(_scope);
            AssertTypeIds(new[] { Subject.TypeId }, new[] { typeId }, $"{_moduleName}.{_name}", languageVersionIs3X, because, reasonArgs);

            return new AndConstraint<PythonTypeAssertions>(this);
        }

        public AndConstraint<PythonTypeAssertions> HaveMemberType(PythonMemberType memberType, string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(Subject is IPythonType m && m.MemberType == memberType)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {_moduleName}.{_name} to be {memberType} {{reason}}.");

            return new AndConstraint<PythonTypeAssertions>(this);
        }
    }
}
