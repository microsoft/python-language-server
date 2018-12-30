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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Tests.FluentAssertions {
    internal sealed class VariableAssertions : ReferenceTypeAssertions<IVariable, VariableAssertions> {
        public IMember Value { get; }
        public VariableAssertions(IVariable v) {
            Subject = v;
            Value = v.Value;
        }

        protected override string Identifier => nameof(IVariable);

        public AndWhichConstraint<VariableAssertions, IVariable> HaveMemberType(PythonMemberType memberType, string because = "", params object[] reasonArgs) {
            Value.Should().HaveMemberType(memberType, because, reasonArgs);
            return new AndWhichConstraint<VariableAssertions, IVariable>(this, Subject);
        }

        public AndWhichConstraint<VariableAssertions, IVariable> HaveType(BuiltinTypeId typeId, string because = "", params object[] reasonArgs) {
            Value.Should().HaveType(typeId, because, reasonArgs);
            return new AndWhichConstraint<VariableAssertions, IVariable>(this, Subject);
        }

        public AndWhichConstraint<VariableAssertions, IVariable> HaveType<TType>(string because = "", params object[] reasonArgs) {
            Value.Should().HaveType(typeof(TType), because, reasonArgs);
            return new AndWhichConstraint<VariableAssertions, IVariable>(this, Subject);
        }

        public AndWhichConstraint<VariableAssertions, IVariable> HaveType(string typeName, string because = "", params object[] reasonArgs) {
            Value.Should().HaveType(typeName, because, reasonArgs);
            return new AndWhichConstraint<VariableAssertions, IVariable>(this, Subject);
        }

        public AndConstraint<VariableAssertions> HaveNoType(string because = "", params object[] reasonArgs) {
            Value.Should().HaveNoType(because, reasonArgs);
            return new AndConstraint<VariableAssertions>(this);
        }

        public AndWhichConstraint<VariableAssertions, IMember> HaveMember(string name, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);
            Value.Should().HaveMember<IMember>(name, because, reasonArgs);
            var m = Value.GetPythonType().GetMember(name);
            return new AndWhichConstraint<VariableAssertions, IMember>(this, m);
        }

        public AndWhichConstraint<VariableAssertions, IMember> HaveMembers(IEnumerable<string> names, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);
            Value.Should().HaveMembers(names, because, reasonArgs);
            return new AndWhichConstraint<VariableAssertions, IMember>(this, Subject);
        }

        public AndWhichConstraint<VariableAssertions, T> HaveMember<T>(string name, string because = "", params object[] reasonArgs)
            where T: class, IPythonType {
            NotBeNull(because, reasonArgs);
            Value.Should().HaveMember<IMember>(name, because, reasonArgs);
            var m = Value.GetPythonType().GetMember(name).GetPythonType<T>();
            return new AndWhichConstraint<VariableAssertions, T>(this, m);
        }

        public AndWhichConstraint<VariableAssertions, IPythonFunctionOverload> HaveOverloadWithParametersAt(int index, string because = "", params object[] reasonArgs) {
            var constraint = HaveOverloadAt(index);
            var overload = constraint.Which;
            var function = Value.GetPythonType<IPythonFunctionType>();

            Execute.Assertion.ForCondition(function != null)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {Subject.Name} to be a function, but it is {Subject.Value}.");

            Execute.Assertion.ForCondition(overload.Parameters.Count > 0)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected overload at index {index} of {function.DeclaringModule.Name}.{function.Name} to have parameters{{reason}}, but it has none.");

            return constraint;
        }

        public AndWhichConstraint<VariableAssertions, IPythonFunctionOverload> HaveOverloadAt(int index, string because = "", params object[] reasonArgs) {
            var function = Subject.Value.GetPythonType<IPythonFunctionType>();

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
            var f = Subject.Value.GetPythonType<IPythonFunctionType>();

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
