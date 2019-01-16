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
using Microsoft.Python.Analysis.Types;
using static Microsoft.Python.Analysis.Tests.FluentAssertions.AssertionsUtilities;

namespace Microsoft.Python.Analysis.Tests.FluentAssertions {
    internal class PythonFunctionOverloadAssertions : ReferenceTypeAssertions<IPythonFunctionOverload, PythonFunctionOverloadAssertions> {
        public PythonFunctionOverloadAssertions(IPythonFunctionOverload pythonFunctionOverload) {
            Subject = pythonFunctionOverload;
        }

        protected override string Identifier => nameof(IPythonFunctionOverload);

        public AndWhichConstraint<PythonFunctionOverloadAssertions, IPythonType> HaveReturnType(string because = "", params object[] reasonArgs) {
            var returnType = Subject.GetReturnValue(LocationInfo.Empty, ArgumentSet.Empty);
            Execute.Assertion.ForCondition(returnType != null)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {Subject.Name} overload to have a return type{{reason}}, but it has none.");

            return new AndWhichConstraint<PythonFunctionOverloadAssertions, IPythonType>(this, returnType.GetPythonType());
        }

        public AndWhichConstraint<PythonFunctionOverloadAssertions, IPythonFunctionOverload> HaveReturnType(BuiltinTypeId typeid, string because = "", params object[] reasonArgs) {
            Subject.GetReturnValue(LocationInfo.Empty, ArgumentSet.Empty).GetPythonType().TypeId.Should().Be(typeid);
            return new AndWhichConstraint<PythonFunctionOverloadAssertions, IPythonFunctionOverload>(this, Subject);
        }

        public AndWhichConstraint<PythonFunctionOverloadAssertions, string> HaveDocumentation(string documentation, string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(Subject.Documentation == documentation)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {Subject.Name} overload to have documentation '{documentation}', but it has '{Subject.Documentation}'.");

            return new AndWhichConstraint<PythonFunctionOverloadAssertions, string>(this, Subject.Documentation);
        }

        public AndWhichConstraint<PythonFunctionOverloadAssertions, string> HaveReturnDocumentation(string documentation, string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(Subject.ReturnDocumentation == documentation)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {Subject.Name} overload to have a return documentation '{documentation}', but it has '{Subject.ReturnDocumentation}'.");

            return new AndWhichConstraint<PythonFunctionOverloadAssertions, string>(this, Subject.ReturnDocumentation);
        }

        public AndWhichConstraint<PythonFunctionOverloadAssertions, IPythonFunctionOverload> HaveName(string name, string because = "", params object[] reasonArgs) {
            Subject.Name.Should().Be(name);
            return new AndWhichConstraint<PythonFunctionOverloadAssertions, IPythonFunctionOverload>(this, Subject);
        }

        public AndWhichConstraint<PythonFunctionOverloadAssertions, IParameterInfo> HaveParameterAt(int index, string because = "", params object[] reasonArgs) {
            var parameters = Subject.Parameters;
            Execute.Assertion.ForCondition(parameters.Count > index)
                .BecauseOf(because, reasonArgs)
                .FailWith(parameters.Count > 0
                    ? $"Expected {Subject.Name} to have parameter at index {index}{{reason}}, but it has only {parameters.Count} parameters."
                    : $"Expected {Subject.Name} to have parameter at index {index}{{reason}}, but it has none.");

            return new AndWhichConstraint<PythonFunctionOverloadAssertions, IParameterInfo>(this, parameters[index]);
        }

        public AndWhichConstraint<PythonFunctionOverloadAssertions, IParameterInfo> HaveSingleParameter(string because = "", params object[] reasonArgs) {
            var parameters = Subject.Parameters;
            Execute.Assertion.ForCondition(parameters.Count == 1)
                .BecauseOf(because, reasonArgs)
                .FailWith(parameters.Count > 0
                    ? $"Expected {Subject.Name} overload to have only one parameter{{reason}}, but it has {parameters.Count} parameters."
                    : $"Expected {Subject.Name} overload to have one parameter{{reason}}, but it has none.");

            return new AndWhichConstraint<PythonFunctionOverloadAssertions, IParameterInfo>(this, parameters[0]);
        }

        public AndConstraint<PythonFunctionOverloadAssertions> HaveParameters(params string[] parameters) => HaveParameters(parameters, string.Empty);

        public AndConstraint<PythonFunctionOverloadAssertions> HaveParameters(IEnumerable<string> parameters, string because = "", params object[] reasonArgs) {
            var current = Subject.Parameters.Select(pr => pr.Name).ToArray();
            var expected = parameters.ToArray();

            var message = GetAssertCollectionOnlyContainsMessage(current, expected, Subject.Name, "parameter", "parameters");
            Execute.Assertion.ForCondition(message == null)
                .BecauseOf(because, reasonArgs)
                .FailWith(message);

            return new AndConstraint<PythonFunctionOverloadAssertions>(this);
        }

        public AndConstraint<PythonFunctionOverloadAssertions> HaveNoParameters(string because = "", params object[] reasonArgs)
            => HaveParameters(Enumerable.Empty<string>(), because, reasonArgs);

        public AndConstraint<PythonFunctionOverloadAssertions> HaveReturnType(string type, string because = "", params object[] reasonArgs) {
            var returnType = Subject.GetReturnValue(LocationInfo.Empty, ArgumentSet.Empty).GetPythonType();
            Execute.Assertion.ForCondition(string.Equals(returnType.Name, type, StringComparison.Ordinal))
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {Subject.Name} to have return type [{type}]{{reason}}, but it has [{returnType}].");

            return new AndConstraint<PythonFunctionOverloadAssertions>(this);
        }
    }
}
