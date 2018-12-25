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
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using static Microsoft.Python.Analysis.Tests.FluentAssertions.AssertionsUtilities;

namespace Microsoft.Python.Analysis.Tests.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal class MemberAssertions : ReferenceTypeAssertions<IMember, MemberAssertions> {
        public MemberAssertions(IMember member) {
            Subject = member;
        }

        protected override string Identifier => nameof(IMember);

        public AndWhichConstraint<MemberAssertions, IPythonClass> HaveClass(string name, string because = "", params object[] reasonArgs)
            => HaveMember<IPythonClass>(name, because, reasonArgs).OfMemberType(PythonMemberType.Class);

        public AndWhichConstraint<MemberAssertions, IPythonFunction> HaveFunction(string name, string because = "", params object[] reasonArgs)
            => HaveMember<IPythonFunction>(name, because, reasonArgs).OfMemberType(PythonMemberType.Function);

        public AndWhichConstraint<MemberAssertions, IPythonProperty> HaveProperty(string name, string because = "", params object[] reasonArgs)
            => HaveMember<IPythonProperty>(name, because, reasonArgs).OfMemberType(PythonMemberType.Property);

        public AndWhichConstraint<MemberAssertions, IPythonProperty> HaveReadOnlyProperty(string name, string because = "", params object[] reasonArgs) {
            var constraint = HaveProperty(name, because, reasonArgs);
            Execute.Assertion.ForCondition(constraint.Which.IsReadOnly)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetName(Subject)} to have a property {name} which is read-only{{reason}}, but it is writable.");

            return constraint;
        }

        public AndWhichConstraint<MemberAssertions, PythonFunction> HaveMethod(string name, string because = "", params object[] reasonArgs)
            => HaveMember<PythonFunction>(name, because, reasonArgs).OfMemberType(PythonMemberType.Method);

        public AndWhichConstraint<MemberAssertions, TMember> HaveMember<TMember>(string name,
            string because = "", params object[] reasonArgs)
            where TMember : class, IMember {
            NotBeNull();

            var t = Subject.GetPythonType();
            var mc =  t as IMemberContainer;
            Execute.Assertion.ForCondition(mc != null)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetName(t)} to be a member container{{reason}}.");

            var member = mc.GetMember(name);
            var typedMember = member as TMember;
            Execute.Assertion.ForCondition(member != null)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetName(t)} to have a member {name}{{reason}}.")
                .Then
                .ForCondition(typedMember != null)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetName(t)} to have a member {name} of type {typeof(TMember)}{{reason}}, but its type is {member?.GetType()}.");

            return new AndWhichConstraint<MemberAssertions, TMember>(this, typedMember);
        }

        public AndConstraint<MemberAssertions> HaveSameMembersAs(IMember m) {
            m.Should().BeAssignableTo<IMemberContainer>();
            return HaveMembers(((IMemberContainer)m).GetMemberNames(), string.Empty);
        }

        public AndConstraint<MemberAssertions> HaveMembers(params string[] memberNames)
            => HaveMembers(memberNames, string.Empty);

        public AndConstraint<MemberAssertions> HaveMembers(IEnumerable<string> memberNames, string because = "", params object[] reasonArgs) {
            var names = Subject.GetPythonType().GetMemberNames().ToArray();
            var expectedNames = memberNames.ToArray();
            var missingNames = expectedNames.Except(names).ToArray();

            Execute.Assertion.ForCondition(missingNames.Length == 0)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetQuotedName(Subject)} to have members with names {GetQuotedNames(expectedNames)}{{reason}}, but haven't found {GetQuotedNames(missingNames)}");

            return new AndConstraint<MemberAssertions>(this);
        }

        public AndConstraint<MemberAssertions> NotHaveMembers(params string[] memberNames)
            => NotHaveMembers(memberNames, string.Empty);

        public AndConstraint<MemberAssertions> NotHaveMembers(IEnumerable<string> memberNames, string because = "", params object[] reasonArgs) {
            var names = Subject.GetPythonType().GetMemberNames();
            var missingNames = memberNames.ToArray();
            var existingNames = names.Intersect(missingNames).ToArray();

            Execute.Assertion.ForCondition(existingNames.Length == 0)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetQuotedName(Subject)} to have no members with names {GetQuotedNames(missingNames)}{{reason}}, but found {GetQuotedNames(existingNames)}");

            return new AndConstraint<MemberAssertions>(this);
        }

        public AndConstraint<MemberAssertions> HaveInstanceType<T>(string because = "", params object[] reasonArgs) {
            var instance = Subject as IPythonInstance;
            Execute.Assertion.ForCondition(instance != null)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetQuotedName(Subject)} be an instance{{reason}}");
            instance.Type.Should().BeAssignableTo<T>();
            return new AndConstraint<MemberAssertions>(this);
        }

        public AndConstraint<MemberAssertions> HaveType(BuiltinTypeId typeId, string because = "", params object[] reasonArgs) {
            var instance = Subject as IPythonInstance;
            Execute.Assertion.ForCondition(instance != null)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetQuotedName(Subject)} be an instance{{reason}}");
            instance.GetPythonType().TypeId.Should().Be(typeId, because, reasonArgs);
            return new AndConstraint<MemberAssertions>(this);
        }
    }
}
