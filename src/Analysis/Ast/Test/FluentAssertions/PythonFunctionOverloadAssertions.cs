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
        private readonly string _functionName;

        public PythonFunctionOverloadAssertions(IPythonFunctionOverload pythonFunctionOverload, string functionName) {
            _functionName = functionName;
            Subject = pythonFunctionOverload;
        }

        protected override string Identifier => nameof(IPythonFunctionOverload);

        public AndWhichConstraint<PythonFunctionOverloadAssertions, IPythonType> HaveSingleReturnType(string because = "", params object[] reasonArgs) {
            var returnTypes = Subject.ReturnType.ToArray();
            Execute.Assertion.ForCondition(returnTypes.Length == 1)
                .BecauseOf(because, reasonArgs)
                .FailWith(returnTypes.Length > 0
                    ? $"Expected {_functionName} overload to have only one return type{{reason}}, but it has {returnTypes.Length} overloads."
                    : $"Expected {_functionName} overload to have a return type{{reason}}, but it has none.");

            return new AndWhichConstraint<PythonFunctionOverloadAssertions, IPythonType>(this, returnTypes[0]);
        }

        public AndWhichConstraint<PythonFunctionOverloadAssertions, IParameterInfo> HaveParameterAt(int index, string because = "", params object[] reasonArgs) {
            var parameters = Subject.GetParameters();
            Execute.Assertion.ForCondition(parameters.Length > index)
                .BecauseOf(because, reasonArgs)
                .FailWith(parameters.Length > 0
                    ? $"Expected {_functionName} to have parameter at index {index}{{reason}}, but it has only {parameters.Length} parameters."
                    : $"Expected {_functionName} to have parameter at index {index}{{reason}}, but it has none.");

            return new AndWhichConstraint<PythonFunctionOverloadAssertions, IParameterInfo>(this, parameters[index]);
        }

        public AndWhichConstraint<PythonFunctionOverloadAssertions, IParameterInfo> HaveSingleParameter(string because = "", params object[] reasonArgs) {
            var parameters = Subject.GetParameters();
            Execute.Assertion.ForCondition(parameters.Length == 1)
                .BecauseOf(because, reasonArgs)
                .FailWith(parameters.Length > 0
                    ? $"Expected {_functionName} overload to have only one parameter{{reason}}, but it has {parameters.Length} parameters."
                    : $"Expected {_functionName} overload to have one parameter{{reason}}, but it has none.");

            return new AndWhichConstraint<PythonFunctionOverloadAssertions, IParameterInfo>(this, parameters[0]);
        }

        public AndConstraint<PythonFunctionOverloadAssertions> HaveParameters(params string[] parameters) => HaveParameters(parameters, string.Empty);

        public AndConstraint<PythonFunctionOverloadAssertions> HaveParameters(IEnumerable<string> parameters, string because = "", params object[] reasonArgs) {
            var current = Subject.GetParameters().Select(pr => pr.Name).ToArray();
            var expected = parameters.ToArray();

            var message = GetAssertCollectionOnlyContainsMessage(current, expected, _functionName, "parameter", "parameters");
            Execute.Assertion.ForCondition(message == null)
                .BecauseOf(because, reasonArgs)
                .FailWith(message);

            return new AndConstraint<PythonFunctionOverloadAssertions>(this);
        }

        public AndConstraint<PythonFunctionOverloadAssertions> HaveNoParameters(string because = "", params object[] reasonArgs)
            => HaveParameters(Enumerable.Empty<string>(), because, reasonArgs);

        public AndConstraint<PythonFunctionOverloadAssertions> HaveSingleReturnType(string type, string because = "", params object[] reasonArgs) {
            var returnTypes = Subject.ReturnType;
            Execute.Assertion.ForCondition(returnTypes.Count == 1)
                .BecauseOf(because, reasonArgs)
                .FailWith(returnTypes.Count > 0
                    ? $"Expected {_functionName} to have only one return type{{reason}}, but it has {returnTypes.Count} return types."
                    : $"Expected {_functionName} to have a return type{{reason}}, but it has none.");

            if (returnTypes.Count == 1) {
                Execute.Assertion.ForCondition(string.Equals(returnTypes[0].Name, type, StringComparison.Ordinal))
                    .BecauseOf(because, reasonArgs)
                    .FailWith($"Expected {_functionName} to have return type [{type}]{{reason}}, but it has [{returnTypes[0]}].");
            }

            return new AndConstraint<PythonFunctionOverloadAssertions>(this);
        }
    }
}
