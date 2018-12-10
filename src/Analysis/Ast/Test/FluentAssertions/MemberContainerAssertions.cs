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
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using Microsoft.Python.Analysis.Analyzer.Modules;
using Microsoft.Python.Analysis.Analyzer.Types;
using static Microsoft.Python.Analysis.Tests.FluentAssertions.AssertionsUtilities;

namespace Microsoft.Python.Analysis.Tests.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal class MemberContainerAssertions<TMemberContainer> : MemberContainerAssertions<IMemberContainer, MemberContainerAssertions<TMemberContainer>> {
        public MemberContainerAssertions(IMemberContainer memberContainer) : base(memberContainer) { }
    }

    [ExcludeFromCodeCoverage]
    internal class MemberContainerAssertions<TMemberContainer, TAssertions> : ReferenceTypeAssertions<TMemberContainer, TAssertions>
        where TMemberContainer : IMemberContainer
        where TAssertions : MemberContainerAssertions<TMemberContainer, TAssertions> {

        public MemberContainerAssertions(TMemberContainer memberContainer) {
            Subject = memberContainer;
        }

        protected override string Identifier => nameof(IMemberContainer);

        public AndWhichConstraint<TAssertions, AstPythonMultipleMembers> HaveMultipleTypesMember(string name, string because = "", params object[] reasonArgs)
            => HaveMember<AstPythonMultipleMembers>(name, because, reasonArgs).OfMemberType(PythonMemberType.Class);

        public AndWhichConstraint<TAssertions, AstPythonType> HaveClass(string name, string because = "", params object[] reasonArgs)
            => HaveMember<AstPythonType>(name, because, reasonArgs).OfMemberType(PythonMemberType.Class);

        public AndWhichConstraint<TAssertions, AstNestedPythonModule> HaveNestedModule(string name, string because = "", params object[] reasonArgs)
            => HaveMember<AstNestedPythonModule>(name, because, reasonArgs).OfMemberType(PythonMemberType.Module);

        public AndWhichConstraint<TAssertions, AstPythonProperty> HaveProperty(string name, string because = "", params object[] reasonArgs)
            => HaveMember<AstPythonProperty>(name, because, reasonArgs).OfMemberType(PythonMemberType.Property);

        public AndWhichConstraint<TAssertions, AstPythonProperty> HaveReadOnlyProperty(string name, string because = "", params object[] reasonArgs) {
            var constraint = HaveProperty(name, because, reasonArgs);
            Execute.Assertion.ForCondition(constraint.Which.IsReadOnly)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetName(Subject)} to have a property {name} which is read-only{{reason}}, but it is writable.");

            return constraint;
        }

        public AndWhichConstraint<TAssertions, AstPythonFunction> HaveMethod(string name, string because = "", params object[] reasonArgs)
            => HaveMember<AstPythonFunction>(name, because, reasonArgs).OfMemberType(PythonMemberType.Method);

        public AndWhichConstraint<TAssertions, TMember> HaveMember<TMember>(string name,
            string because = "", params object[] reasonArgs)
            where TMember : class, IMember {
            NotBeNull();

            var member = Subject.GetMember(name);
            var typedMember = member as TMember;
            Execute.Assertion.ForCondition(member != null)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetName(Subject)} to have a member {name}{{reason}}.")
                .Then
                .ForCondition(typedMember != null)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetName(Subject)} to have a member {name} of type {typeof(TMember)}{{reason}}, but its type is {member?.GetType()}.");

            return new AndWhichConstraint<TAssertions, TMember>((TAssertions)this, typedMember);
        }

        public AndConstraint<TAssertions> HaveMembers(params string[] memberNames)
            => HaveMembers(memberNames, string.Empty);

        public AndConstraint<TAssertions> HaveMembers(IEnumerable<string> memberNames, string because = "", params object[] reasonArgs) {
            var names = Subject.GetMemberNames().ToArray();
            var expectedNames = memberNames.ToArray();
            var missingNames = expectedNames.Except(names).ToArray();

            Execute.Assertion.ForCondition(missingNames.Length == 0)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetQuotedName(Subject)} to have members with names {GetQuotedNames(expectedNames)}{{reason}}, but haven't found {GetQuotedNames(missingNames)}");

            return new AndConstraint<TAssertions>((TAssertions)this);
        }

        public AndConstraint<TAssertions> NotHaveMembers(params string[] memberNames)
            => NotHaveMembers(memberNames, string.Empty);

        public AndConstraint<TAssertions> NotHaveMembers(IEnumerable<string> memberNames, string because = "", params object[] reasonArgs) {
            var names = Subject.GetMemberNames();
            var missingNames = memberNames.ToArray();
            var existingNames = names.Intersect(missingNames).ToArray();

            Execute.Assertion.ForCondition(existingNames.Length == 0)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetQuotedName(Subject)} to have no members with names {GetQuotedNames(missingNames)}{{reason}}, but found {GetQuotedNames(existingNames)}");

            return new AndConstraint<TAssertions>((TAssertions)this);
        }
    }
}
