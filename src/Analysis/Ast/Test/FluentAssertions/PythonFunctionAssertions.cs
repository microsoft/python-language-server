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
using static Microsoft.Python.Analysis.Tests.FluentAssertions.AssertionsUtilities;

namespace Microsoft.Python.Analysis.Tests.FluentAssertions {
    internal class PythonFunctionAssertions : ReferenceTypeAssertions<IPythonFunctionType, PythonFunctionAssertions> {
        public PythonFunctionAssertions(IPythonFunctionType pythonFunction) {
            Subject = pythonFunction;
            ScopeDescription = $"in a scope {GetQuotedName(Subject.DeclaringType)}";
        }

        protected override string Identifier => nameof(IPythonFunctionType);
        protected string ScopeDescription { get; }

        public AndConstraint<PythonFunctionAssertions> BeClassMethod(string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(Subject.IsClassMethod)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {Subject.Name} to be a class method{{reason}}");

            return new AndConstraint<PythonFunctionAssertions>(this);
        }

        public AndConstraint<PythonFunctionAssertions> HaveOverloads(string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(Subject.Overloads.Any())
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetName()} to have overloads{{reason}}.");

            return new AndConstraint<PythonFunctionAssertions>(this);
        }

        public AndConstraint<PythonFunctionAssertions> HaveOverloadCount(int count, string because = "", params object[] reasonArgs) {
            var overloads = Subject.Overloads.ToArray();
            Execute.Assertion.ForCondition(overloads.Length == count)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetName()} to have {GetOverloadsString(count)}{{reason}}, but it {GetOverloadsString(overloads.Length)}.");

            return new AndConstraint<PythonFunctionAssertions>(this);
        }

        public AndWhichConstraint<PythonFunctionAssertions, IPythonFunctionOverload> HaveSingleOverload(string because = "", params object[] reasonArgs) {
            var overloads = Subject.Overloads.ToArray();
            Execute.Assertion.ForCondition(overloads.Length == 1)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetName()} to have single overload{{reason}}, but it {GetOverloadsString(overloads.Length)}.");

            return new AndWhichConstraint<PythonFunctionAssertions, IPythonFunctionOverload>(this, overloads[0]);
        }

        public AndWhichConstraint<PythonFunctionAssertions, IPythonFunctionOverload> HaveOverloadAt(int index, string because = "", params object[] reasonArgs) {
            var overloads = Subject.Overloads.ToArray();
            Execute.Assertion.ForCondition(overloads.Length > index)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetName()} to have overload at index {index}{{reason}}, but it {GetOverloadsString(overloads.Length)}.");

            return new AndWhichConstraint<PythonFunctionAssertions, IPythonFunctionOverload>(this, overloads[index]);
        }

        public AndWhichConstraint<PythonFunctionOverloadAssertions, IParameterInfo> HaveParameterAt(int index, string because = "", params object[] reasonArgs) {
            var overloads = Subject.Overloads.ToArray();
            Execute.Assertion.ForCondition(overloads.Length == 1)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetName()} to have a single overload {{reason}}, but it has {GetOverloadsString(overloads.Length)}.");

            var o = overloads[0];
            Execute.Assertion.ForCondition(o.Parameters.Count > index)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetName()} to have parameter at index {index}{{reason}}, but it only has {GetOverloadsString(o.Parameters.Count)}.");

            var oa = new PythonFunctionOverloadAssertions(o);
            return new AndWhichConstraint<PythonFunctionOverloadAssertions, IParameterInfo>(oa, o.Parameters[index]);
        }

        public AndWhichConstraint<VariableAssertions, IVariable> HaveVariable(string name, string because = "", params object[] reasonArgs) {
            var scope = Subject.GetScope();
            Execute.Assertion.ForCondition(scope != null)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetName()} to have associated scope {{reason}}, but it has none.");

            var v = scope.Variables[name];
            Execute.Assertion.ForCondition(v != null)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {GetName()} to have variable {name} {{reason}}, but it has none.");

            var va = new VariableAssertions(v);
            return new AndWhichConstraint<VariableAssertions, IVariable>(va, v);
        }


        private static string GetOverloadsString(int overloadsCount)
            => overloadsCount > 1
                ? $"has {overloadsCount} overloads"
                : overloadsCount > 0
                    ? "has only one overload"
                    : "has no overloads";

        protected virtual string GetName() => $"{GetQuotedName(Subject)} {ScopeDescription}";
    }
}
