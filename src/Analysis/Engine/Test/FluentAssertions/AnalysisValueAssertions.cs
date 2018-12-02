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

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using static Microsoft.PythonTools.Analysis.FluentAssertions.AssertionsUtilities;

namespace Microsoft.PythonTools.Analysis.FluentAssertions {
    internal class AnalysisValueAssertions<TAnalysisValue> : AnalysisValueAssertions<TAnalysisValue, AnalysisValueAssertions<TAnalysisValue>>
        where TAnalysisValue : IAnalysisValue {

        public AnalysisValueAssertions(AnalysisValueTestInfo<TAnalysisValue> subject) : base(subject) { }
    }

    internal class AnalysisValueAssertions<TAnalysisValue, TAssertions> : ReferenceTypeAssertions<TAnalysisValue, TAssertions>
        where TAnalysisValue : IAnalysisValue
        where TAssertions : AnalysisValueAssertions<TAnalysisValue, TAssertions> {

        protected IScope OwnerScope { get; }
        protected string ScopeDescription { get; }

        public AnalysisValueAssertions(AnalysisValueTestInfo<TAnalysisValue> subject) {
            OwnerScope = subject.OwnerScope;
            ScopeDescription = subject.ScopeDescription ?? $"in a scope {GetQuotedName(OwnerScope)}";
            Subject = subject;
        }

        protected override string Identifier => nameof(IAnalysisValue);

        public AndConstraint<TAssertions> HaveName(string name, string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(string.Equals(Subject.Name, name, StringComparison.Ordinal))
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetName()} to have name '{name}'{{reason}}.");

            return new AndConstraint<TAssertions>((TAssertions)this);
        }

        public AndConstraint<TAssertions> HaveType(BuiltinTypeId typeId, string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(Subject.TypeId == typeId)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetName()} to be {typeId}{{reason}}, but it is {Subject.TypeId}.");

            return new AndConstraint<TAssertions>((TAssertions)this);
        }

        public AndConstraint<TAssertions> HaveDescription(string description, string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(string.Equals(Subject.Description, description, StringComparison.Ordinal))
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetName()} to be {description}{{reason}}, but it is {Subject.Description}.");

            return new AndConstraint<TAssertions>((TAssertions)this);
        }

        public AndConstraint<TAssertions> HaveOnlyMembers(params string[] memberNames)
            => HaveOnlyMembers(memberNames, string.Empty);

        public AndConstraint<TAssertions> HaveOnlyMembers(IEnumerable<string> memberNames, string because = "", params object[] reasonArgs) {
            var actualNames = Subject.GetAllMembers(ModuleContext).Keys.ToArray();
            var expectedNames = memberNames.ToArray();

            var errorMessage = GetAssertCollectionOnlyContainsMessage(actualNames, expectedNames, GetName(), "member", "members");

            Execute.Assertion.ForCondition(errorMessage == null)
                .BecauseOf(because, reasonArgs)
                .FailWith(errorMessage);

            return new AndConstraint<TAssertions>((TAssertions)this);
        }

        public AndConstraint<TAssertions> HaveMembers(params string[] memberNames)
            => HaveMembers(memberNames, string.Empty);

        public AndConstraint<TAssertions> HaveMembers(IEnumerable<string> memberNames, string because = "", params object[] reasonArgs) {
            var actualNames = Subject.GetAllMembers(ModuleContext).Keys.ToArray();
            var expectedNames = memberNames.ToArray();

            var errorMessage = GetAssertCollectionContainsMessage(actualNames, expectedNames, GetName(), "member", "members");

            Execute.Assertion.ForCondition(errorMessage == null)
                .BecauseOf(because, reasonArgs)
                .FailWith(errorMessage);

            return new AndConstraint<TAssertions>((TAssertions)this);
        }

        public AndConstraint<TAssertions> NotHaveMembers(params string[] memberNames)
            => NotHaveMembers(memberNames, string.Empty);

        public AndConstraint<TAssertions> NotHaveMembers(IEnumerable<string> memberNames, string because = "", params object[] reasonArgs) {
            var actualNames = Subject.GetAllMembers(ModuleContext).Keys.ToArray();
            var expectedNames = memberNames.ToArray();

            var errorMessage = GetAssertCollectionNotContainMessage(actualNames, expectedNames, GetName(), "member", "members");

            Execute.Assertion.ForCondition(errorMessage == null)
                .BecauseOf(because, reasonArgs)
                .FailWith(errorMessage);

            return new AndConstraint<TAssertions>((TAssertions)this);
        }

        public AndConstraint<TAssertions> HaveMemberOfType(string memberName, BuiltinTypeId typeId)
            => HaveMemberOfTypes(memberName, typeId);

        public AndConstraint<TAssertions> HaveMemberOfTypes(string memberName, params BuiltinTypeId[] typeIds)
            => HaveMemberOfTypes(memberName, typeIds, string.Empty);

        public AndConstraint<TAssertions> HaveMemberOfTypes(string memberName, IEnumerable<BuiltinTypeId> typeIds, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);

            Execute.Assertion.BecauseOf(because, reasonArgs)
                .AssertHasMember(Subject, OwnerScope, memberName, GetName(), $"member '{memberName}'", out var member);

            AssertTypeIds(member, typeIds, memberName, Is3X(OwnerScope), because, reasonArgs);
            return new AndConstraint<TAssertions>((TAssertions)this);
        }

        public AndConstraint<TAssertions> HaveMemberType(PythonMemberType memberType, string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(Subject.MemberType == memberType)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetName()} to be {memberType}{{reason}}, but it is {Subject.MemberType}.");

            return new AndConstraint<TAssertions>((TAssertions)this);
        }

        public AndWhichConstraint<TAssertions, IPythonType> HavePythonType(IPythonType pythonType, string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(Equals(Subject.PythonType, pythonType))
                .BecauseOf(because, reasonArgs)
                .FailWith(Subject.PythonType != null
                    ? $"Expected {GetName()} to be {GetQuotedName(pythonType)}{{reason}}, but it is {GetQuotedName(Subject.PythonType)}."
                    : $"Expected {GetName()} to be {GetQuotedName(pythonType)}{{reason}}, but it is null.");

            return new AndWhichConstraint<TAssertions, IPythonType>((TAssertions)this, pythonType);
        }

        public AndWhichConstraint<TAssertions, IEnumerable<IPythonType>> HavePythonTypes(IEnumerable<IPythonType> pythonTypes, string because = "", params object[] reasonArgs) {
            var members = Subject.GetAllMembers(null).OfType<IPythonType>();

            Execute.Assertion.ForCondition(Subject.MemberType == PythonMemberType.Multiple)
                .BecauseOf(because, reasonArgs)
                .FailWith(Subject.PythonType != null
                    ? $"Expected {GetName()} to be {'a'}{{reason}}, but it is {GetQuotedName(Subject.PythonType)}."
                    : $"Expected {GetName()} to be {'b'}{{reason}}, but it is null.");

            return new AndWhichConstraint<TAssertions, IEnumerable<IPythonType>>((TAssertions)this, members);
        }

        public AndConstraint<TAssertions> HaveOverloads(string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(Subject.Overloads.Any())
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetName()} to have overloads{{reason}}.");

            return new AndConstraint<TAssertions>((TAssertions)this);
        }

        public AndConstraint<TAssertions> HaveOverloadCount(int count, string because = "", params object[] reasonArgs) {
            var overloads = Subject.Overloads.ToArray();
            Execute.Assertion.ForCondition(overloads.Length == count)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetName()} to have {GetOverloadsString(count)}{{reason}}, but it {GetOverloadsString(overloads.Length)}.");

            return new AndConstraint<TAssertions>((TAssertions)this);
        }

        public AndWhichConstraint<TAssertions, OverloadResultTestInfo> HaveSingleOverload(string because = "", params object[] reasonArgs) {
            var overloads = Subject.Overloads.ToArray();
            Execute.Assertion.ForCondition(overloads.Length == 1)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetName()} to have single overload{{reason}}, but it {GetOverloadsString(overloads.Length)}.");

            return new AndWhichConstraint<TAssertions, OverloadResultTestInfo>((TAssertions)this, new OverloadResultTestInfo(overloads[0], GetOverloadName(overloads[0].Name)));
        }

        public AndWhichConstraint<TAssertions, OverloadResultTestInfo> HaveOverloadAt(int index, string because = "", params object[] reasonArgs) {
            var overloads = Subject.Overloads.ToArray();
            Execute.Assertion.ForCondition(overloads.Length > index)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetName()} to have overload at index {index}{{reason}}, but it {GetOverloadsString(overloads.Length)}.");

            return new AndWhichConstraint<TAssertions, OverloadResultTestInfo>((TAssertions)this, new OverloadResultTestInfo(overloads[index], GetOverloadName(overloads[index].Name)));
        }

        private static string GetOverloadsString(int overloadsCount)
            => overloadsCount > 1
                ? $"has {overloadsCount} overloads"
                : overloadsCount > 0
                    ? "has only one overload"
                    : "has no overloads";

        public AndWhichConstraint<TAssertions, AnalysisValueTestInfo<TMember>> HaveMember<TMember>(string name, string because = "", params object[] reasonArgs)
            where TMember : class, IAnalysisValue {
            NotBeNull(because, reasonArgs);

            Execute.Assertion.BecauseOf(because, reasonArgs)
                .AssertHasMemberOfType(Subject, OwnerScope, name, GetName(), $"member '{name}'", out TMember typedMember);
            return new AndWhichConstraint<TAssertions, AnalysisValueTestInfo<TMember>>((TAssertions)this, new AnalysisValueTestInfo<TMember>(typedMember, null, OwnerScope));
        }

        protected virtual string GetName() => $"{GetQuotedName(Subject)} {ScopeDescription}";

        private string GetOverloadName(string overload) => $"'{overload}' overload {ScopeDescription}";
        private IModuleContext ModuleContext => ((ModuleScope)OwnerScope.GlobalScope).Module.InterpreterContext;
    }
}