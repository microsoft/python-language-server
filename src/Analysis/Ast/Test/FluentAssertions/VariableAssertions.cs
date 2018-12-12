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
using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using Microsoft.Python.Analysis.Analyzer;
using static Microsoft.Python.Analysis.Tests.FluentAssertions.AssertionsUtilities;

namespace Microsoft.Python.Analysis.Tests.FluentAssertions {
    internal sealed class VariableTestInfo {
        private readonly IScope _scope;
        public string Name { get; }
        public IPythonType Type { get; }

        public VariableTestInfo(string name, IPythonType type, IScope scope) {
            Name = name;
            Type = type;
            _scope = scope;
        }

        public VariableAssertions Should() => new VariableAssertions(new Variable(Name, Type), Name, _scope);
    }

    internal sealed class VariableAssertions : ReferenceTypeAssertions<IVariable, VariableAssertions> {
        private readonly string _moduleName;
        private readonly string _name;
        private readonly IScope _scope;

        public VariableAssertions(IVariable v, string name, IScope scope) {
            Subject = v;
            _name = name;
            _scope = scope;
            _moduleName = scope.Name;
        }

        protected override string Identifier => nameof(IVariable);

        public AndConstraint<VariableAssertions> HaveType(BuiltinTypeId typeId, string because = "", params object[] reasonArgs) {
            var languageVersionIs3X = Is3X(_scope);
            AssertTypeIds(new[] { Subject.Type.TypeId }, new[] { typeId }, $"{_moduleName}.{_name}", languageVersionIs3X, because, reasonArgs);

            return new AndConstraint<VariableAssertions>(this);
        }

        public AndConstraint<VariableAssertions> HaveTypes(IEnumerable<BuiltinTypeId> typeIds, string because = "", params object[] reasonArgs) {
            var languageVersionIs3X = Is3X(_scope);
            var actualTypeIds = Subject.Type is IPythonMultipleTypes mt ? mt.GetTypes().Select(t => t.TypeId) : new[] { Subject.Type.TypeId };

            AssertTypeIds(actualTypeIds, typeIds, $"{_moduleName}.{_name}", languageVersionIs3X, because, reasonArgs);
            return new AndConstraint<VariableAssertions>(this);
        }

        public AndConstraint<VariableAssertions> HaveMemberType(PythonMemberType memberType, string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(Subject.Type is IPythonType av && av.MemberType == memberType)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {_moduleName}.{_name} to be {memberType} {{reason}}.");

            return new AndConstraint<VariableAssertions>(this);
        }

        public AndConstraint<VariableAssertions> HaveNoTypes(string because = "", params object[] reasonArgs) {
            var languageVersionIs3X = Is3X(_scope);
            var types = Subject.Type is IPythonMultipleTypes mt ? mt.GetTypes().Select(t => t.TypeId) : new[] { Subject.Type.TypeId };
            AssertTypeIds(types, new BuiltinTypeId[0], $"{_moduleName}.{_name}", languageVersionIs3X, because, reasonArgs);

            return new AndConstraint<VariableAssertions>(this);
        }

        public AndConstraint<VariableAssertions> HaveClassNames(IEnumerable<string> classNames, string because = "", params object[] reasonArgs) {
            var types = Subject.Type is IPythonMultipleTypes mt ? mt.GetTypes().ToArray() : new[] { Subject.Type };

            var actualMemberTypes = types.Select(av => av.MemberType).ToArray();
            var expectedMemberTypes = new[] { PythonMemberType.Class };
            var actualNames = types.Select(av => av.Name).ToArray();
            var expectedNames = classNames.ToArray();

            var message = GetAssertCollectionContainsMessage(actualMemberTypes, expectedMemberTypes, $"variable '{_moduleName}.{_name}'", "member type", "member types")
                ?? GetAssertCollectionOnlyContainsMessage(actualNames, actualNames, $"variable '{_moduleName}.{_name}'", "type", "types");

            Execute.Assertion.ForCondition(message == null)
                    .BecauseOf(because, reasonArgs)
                    .FailWith(message);

            return new AndConstraint<VariableAssertions>(this);
        }

        public AndWhichConstraint<VariableAssertions, TMember> HaveMember<TMember>(string name, string because = "", params object[] reasonArgs)
            where TMember : class, IPythonType {
            NotBeNull(because, reasonArgs);

            Execute.Assertion.BecauseOf(because, reasonArgs)
                .AssertHasMemberOfType(Subject.Type, name, Subject.Name, $"member '{name}'", out TMember typedMember);
            return new AndWhichConstraint<VariableAssertions, TMember>(this, typedMember);
        }

        public AndWhichConstraint<VariableAssertions, IPythonFunctionOverload> HaveOverloadWithParametersAt(int index, string because = "", params object[] reasonArgs) {
            var constraint = HaveOverloadAt(index);
            var overload = constraint.Which;
            var function = Subject.Type as IPythonFunction;

            Execute.Assertion.ForCondition(overload.GetParameters().Length > 0)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected overload at index {index} of {function.DeclaringModule.Name}.{function.Name} to have parameters{{reason}}, but it has none.");

            return constraint;
        }

        public AndWhichConstraint<VariableAssertions, IPythonFunctionOverload> HaveOverloadAt(int index, string because = "", params object[] reasonArgs) {
            var f = Subject.Type as IPythonFunction;
            Execute.Assertion.ForCondition(f != null)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {Subject.Name} to be a function, but it is {Subject.Type}.");

            var overloads = f.Overloads.ToArray();
            Execute.Assertion.ForCondition(overloads.Length > index)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {f.Name} to have overload at index {index}{{reason}}, but it {GetOverloadsString(overloads.Length)}.");

            return new AndWhichConstraint<VariableAssertions, IPythonFunctionOverload>(this, f.Overloads[index]);
        }

        private static string GetOverloadsString(int overloadsCount)
            => overloadsCount > 1
                ? $"has {overloadsCount} overloads"
                : overloadsCount > 0
                    ? "has only one overload"
                    : "has no overloads";

        public AndWhichConstraint<VariableAssertions, IPythonFunctionOverload> HaveSingleOverload(string because = "", params object[] reasonArgs) {
            var f = Subject.Type as IPythonFunction;
            var overloads = f.Overloads.ToArray();
            Execute.Assertion.ForCondition(overloads.Length == 1)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {Subject.Name} to have single overload{{reason}}, but it {GetOverloadsString(overloads.Length)}.");

            return new AndWhichConstraint<VariableAssertions, IPythonFunctionOverload>(this, overloads[0]);
        }
    }
}
