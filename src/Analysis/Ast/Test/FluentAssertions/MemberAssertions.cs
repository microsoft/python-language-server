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
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using static Microsoft.Python.Analysis.Tests.FluentAssertions.AssertionsUtilities;

namespace Microsoft.Python.Analysis.Tests.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal class MemberAssertions : ReferenceTypeAssertions<IMember, MemberAssertions> {
        private IPythonType Type { get; }
        public MemberAssertions(IMember member) {
            Subject = member;
            Type = Subject.GetPythonType();
        }

        protected override string Identifier => nameof(IMember);

        public AndWhichConstraint<MemberAssertions, IPythonClassType> HaveClass(string name, string because = "", params object[] reasonArgs)
            => HaveMember<IPythonClassType>(name, because, reasonArgs).OfMemberType(PythonMemberType.Class);

        public AndWhichConstraint<MemberAssertions, IPythonFunctionType> HaveFunction(string name, string because = "", params object[] reasonArgs)
            => HaveMember<IPythonFunctionType>(name, because, reasonArgs).OfMemberType(PythonMemberType.Function);

        public AndWhichConstraint<MemberAssertions, IPythonPropertyType> HaveProperty(string name, string because = "", params object[] reasonArgs)
            => HaveMember<IPythonPropertyType>(name, because, reasonArgs).OfMemberType(PythonMemberType.Property);

        public AndWhichConstraint<MemberAssertions, IPythonPropertyType> HaveReadOnlyProperty(string name, string because = "", params object[] reasonArgs) {
            var constraint = HaveProperty(name, because, reasonArgs);
            Execute.Assertion.ForCondition(constraint.Which.IsReadOnly)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetName(Subject)} to have a property {name} which is read-only{{reason}}, but it is writable.");

            return constraint;
        }

        public AndWhichConstraint<MemberAssertions, IMember> HaveDocumentation(string documentation, string because = "", params object[] reasonArgs) {
            var t = Subject.GetPythonType();
            Execute.Assertion.ForCondition(t.Documentation == documentation)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetName(t)} to have documentation {documentation}, but it has {t.Documentation}.");

            return new AndWhichConstraint<MemberAssertions, IMember>(this, Subject);
        }

        public AndWhichConstraint<MemberAssertions, IPythonType> HaveBase(string name, string because = "", params object[] reasonArgs) {
            NotBeNull();

            var cls = Subject.GetPythonType<IPythonClassType>();
            Execute.Assertion.ForCondition(cls != null)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetName(cls)} to be a class{{reason}}.");

            var baseType = cls.Bases.OfType<IPythonType>().FirstOrDefault(b => b.Name == name);
            Execute.Assertion.ForCondition(baseType != null)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetName(cls)} to have base {name}{{reason}}.");

            return new AndWhichConstraint<MemberAssertions, IPythonType>(this, baseType);
        }

        public AndWhichConstraint<MemberAssertions, PythonFunctionType> HaveMethod(string name, string because = "", params object[] reasonArgs)
            => HaveMember<PythonFunctionType>(name, because, reasonArgs).OfMemberType(PythonMemberType.Method);

        public AndWhichConstraint<MemberAssertions, TMember> HaveMember<TMember>(string name,
            string because = "", params object[] reasonArgs)
            where TMember : class, IMember {
            NotBeNull();

            var t = Subject.GetPythonType();
            var mc = (IMemberContainer)t;
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

        public AndConstraint<MemberAssertions> HaveSameMemberNamesAs(IMember member) {
            member.Should().BeAssignableTo<IMemberContainer>();
            return HaveMembers(((IMemberContainer)member).GetMemberNames(), string.Empty);
        }

        public void HaveSameMembersAs(IMember other) {
            other.Should().BeAssignableTo<IMemberContainer>();
            var otherContainer = (IMemberContainer)other;

            var subjectType = Subject.GetPythonType();
            var subjectMemberNames = subjectType.GetMemberNames().ToArray();
            var otherMemberNames = otherContainer.GetMemberNames().ToArray();

            var missingNames = otherMemberNames.Except(subjectMemberNames).ToArray();
            var extraNames = subjectMemberNames.Except(otherMemberNames).ToArray();

            missingNames.Should().BeEmpty("Subject has missing names: ", missingNames);
            extraNames.Should().BeEmpty("Subject has extra names: ", extraNames);

            foreach (var n in subjectMemberNames.Except(Enumerable.Repeat("__base__", 1))) {
                var subjectMember = subjectType.GetMember(n);
                var otherMember = otherContainer.GetMember(n);
                var subjectMemberType = subjectMember.GetPythonType();
                var otherMemberType = otherMember.GetPythonType();

                // PythonConstant, PythonUnicodeStrings... etc are mapped to instances.
                if (subjectMember is IPythonInstance) {
                    otherMember.Should().BeAssignableTo<IPythonInstance>();
                }

                subjectMemberType.MemberType.Should().Be(otherMemberType.MemberType, $"Type name: {subjectMemberType.Name}");
                //Debug.Assert(subjectMemberType.MemberType == otherMemberType.MemberType);

                if (subjectMemberType is IPythonClassType subjectClass) {
                    var otherClass = otherMemberType as IPythonClassType;
                    otherClass.Should().NotBeNull();

                    if(subjectClass is IGenericType gt) {
                        otherClass.Should().BeAssignableTo<IGenericType>();
                        otherClass.IsGeneric.Should().Be(gt.IsGeneric, $"Class name: {subjectClass.Name}");
                    }

                    // See https://github.com/microsoft/python-language-server/issues/1533 on unittest.
                    //Debug.Assert(subjectClass.Bases.Count == otherClass.Bases.Count);
                    //subjectClass.Bases.Count.Should().BeGreaterOrEqualTo(otherClass.Bases.Count);
                }

                if (string.IsNullOrEmpty(subjectMemberType.Documentation)) {
                    otherMemberType.Documentation.Should().BeNullOrEmpty($"Type name: {subjectMemberType.Name}.");
                } else {
                    //Debug.Assert(subjectMemberType.Documentation == otherMemberType.Documentation);
                    subjectMemberType.Documentation.Should().Be(otherMemberType.Documentation, $"Type name: {subjectMemberType.Name}.");
                }

                switch (subjectMemberType.MemberType) {
                    case PythonMemberType.Class:
                        // Restored collections (like instance of tuple) turn into classes
                        // rather than into collections with content since we don't track
                        // collection content in libraries. We don't compare qualified names
                        // since original module may be source or a stub and that is not
                        // preserved during restore.
                        // subjectMemberType.QualifiedName.Should().Be(otherMemberType.QualifiedName);
                        break;
                    case PythonMemberType.Function:
                    case PythonMemberType.Method:
                        subjectMemberType.Should().BeAssignableTo<IPythonFunctionType>();
                        otherMemberType.Should().BeAssignableTo<IPythonFunctionType>();
                        if (subjectMemberType is IPythonFunctionType subjectFunction) {
                            var otherFunction = (IPythonFunctionType)otherMemberType;
                            subjectFunction.Should().HaveSameOverloadsAs(otherFunction);
                        }

                        break;
                    case PythonMemberType.Property:
                        subjectMemberType.Should().BeAssignableTo<IPythonPropertyType>();
                        otherMemberType.Should().BeAssignableTo<IPythonPropertyType>();
                        break;
                    case PythonMemberType.Unknown:
                        subjectMemberType.IsUnknown().Should().BeTrue();
                        break;
                }
            }
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

        public AndConstraint<MemberAssertions> HaveType(Type t, string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(Type != null)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetQuotedName(Subject)} to have type{{reason}}");
            Type.Should().BeAssignableTo(t, because, reasonArgs);
            return new AndConstraint<MemberAssertions>(this);
        }

        public AndConstraint<MemberAssertions> HaveType(BuiltinTypeId typeId, string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(Type != null)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetQuotedName(Subject)} to have type{{reason}}");
            Type.TypeId.Should().Be(typeId, because, reasonArgs);
            return new AndConstraint<MemberAssertions>(this);
        }

        public AndConstraint<MemberAssertions> HaveType(string typeName, string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(Type != null)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetQuotedName(Subject)} to have type{{reason}}");
            Type.Name.Should().Be(typeName, because, reasonArgs);
            return new AndConstraint<MemberAssertions>(this);
        }
        public AndConstraint<MemberAssertions> HaveNoType(string because = "", params object[] reasonArgs) {
            Type.IsUnknown().Should().BeTrue(because, reasonArgs);
            return new AndConstraint<MemberAssertions>(this);
        }

        public AndConstraint<MemberAssertions> HaveMemberType(PythonMemberType memberType, string because = "", params object[] reasonArgs) {
            Subject.MemberType.Should().Be(memberType, because, reasonArgs);
            return new AndConstraint<MemberAssertions>(this);
        }
    }
}
