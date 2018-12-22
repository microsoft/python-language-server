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

using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Tests.FluentAssertions {
    internal sealed class VariableAssertions : ReferenceTypeAssertions<IVariable, VariableAssertions> {
        public VariableAssertions(IVariable v) {
            Subject = v;
        }

        protected override string Identifier => nameof(IVariable);

        public AndWhichConstraint<VariableAssertions, IVariable> OfType(PythonMemberType memberType, string because = "", params object[] reasonArgs) {
            Subject.Value.MemberType.Should().Be(memberType);
            return new AndWhichConstraint<VariableAssertions, IVariable>(this, Subject);
        }

        public AndWhichConstraint<VariableAssertions, IVariable> OfType(BuiltinTypeId typeId, string because = "", params object[] reasonArgs) {
            Subject.Value.GetPythonType().TypeId.Should().Be(typeId);
            return new AndWhichConstraint<VariableAssertions, IVariable>(this, Subject);
        }

        public AndWhichConstraint<VariableAssertions, IVariable> OfType<TType>(string because = "", params object[] reasonArgs) {
            Subject.Value.Should().BeAssignableTo<TType>();
            return new AndWhichConstraint<VariableAssertions, IVariable>(this, Subject);
        }

        public AndWhichConstraint<VariableAssertions, IVariable> OfType(string typeName, string because = "", params object[] reasonArgs) {
            Subject.Value.GetPythonType().Name.Should().Be(typeName);
            return new AndWhichConstraint<VariableAssertions, IVariable>(this, Subject);
        }

        public AndConstraint<VariableAssertions> HaveNoType(string because = "", params object[] reasonArgs) {
            Subject.Value.GetPythonType().IsUnknown().Should().BeTrue(because, reasonArgs);
            return new AndConstraint<VariableAssertions>(this);
        }

        public AndWhichConstraint<VariableAssertions, TMember> HaveMember<TMember>(string name, string because = "", params object[] reasonArgs)
            where TMember : class, IPythonType {
            NotBeNull(because, reasonArgs);

            var t = Subject.Value.GetPythonType();
            Execute.Assertion.ForCondition(t != null)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {Subject.Name} to have a value, but the value is {t}.");

            Execute.Assertion.BecauseOf(because, reasonArgs)
                .AssertHasMemberOfType(t, name, Subject.Name, $"member '{name}'", out TMember typedMember);
            return new AndWhichConstraint<VariableAssertions, TMember>(this, typedMember);
        }

        public AndWhichConstraint<VariableAssertions, IPythonFunctionOverload> HaveOverloadWithParametersAt(int index, string because = "", params object[] reasonArgs) {
            var constraint = HaveOverloadAt(index);
            var overload = constraint.Which;
            var function = Subject.Value as IPythonFunction;

            Execute.Assertion.ForCondition(function != null)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {Subject.Name} to be a function, but it is {Subject.Value}.");

            Execute.Assertion.ForCondition(overload.Parameters.Count > 0)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected overload at index {index} of {function.DeclaringModule.Name}.{function.Name} to have parameters{{reason}}, but it has none.");

            return constraint;
        }

        public AndWhichConstraint<VariableAssertions, IPythonFunctionOverload> HaveOverloadAt(int index, string because = "", params object[] reasonArgs) {
            var function = Subject.Value as IPythonFunction;

            Execute.Assertion.ForCondition(function != null)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {Subject.Name} to be a function, but it is {Subject.Value}.");

            var overloads = function.Overloads.ToArray();
            Execute.Assertion.ForCondition(overloads.Length > index)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {function.Name} to have overload at index {index}{{reason}}, but it {GetOverloadsString(overloads.Length)}.");

            return new AndWhichConstraint<VariableAssertions, IPythonFunctionOverload>(this, function.Overloads[index]);
        }

        private static string GetOverloadsString(int overloadsCount)
            => overloadsCount > 1
                ? $"has {overloadsCount} overloads"
                : overloadsCount > 0
                    ? "has only one overload"
                    : "has no overloads";

        public AndWhichConstraint<VariableAssertions, IPythonFunctionOverload> HaveSingleOverload(string because = "", params object[] reasonArgs) {
            var f = Subject.Value as IPythonFunction;

            Execute.Assertion.ForCondition(f != null)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {Subject.Name} to be a function{{reason}}, but it is {Subject.Value}.");

            var overloads = f.Overloads.ToArray();
            Execute.Assertion.ForCondition(overloads.Length == 1)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {Subject.Name} to have single overload{{reason}}, but it {GetOverloadsString(overloads.Length)}.");

            return new AndWhichConstraint<VariableAssertions, IPythonFunctionOverload>(this, overloads[0]);
        }
    }
}
