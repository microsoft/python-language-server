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
using static Microsoft.PythonTools.Analysis.FluentAssertions.AssertionsUtilities;

namespace Microsoft.PythonTools.Analysis.FluentAssertions {
    internal class OverloadResultAssertions : ReferenceTypeAssertions<IOverloadResult, OverloadResultAssertions> {
        private readonly string _name;

        public OverloadResultAssertions(IOverloadResult overloadResult, string name) {
            _name = name;
            Subject = overloadResult;
        }

        protected override string Identifier => nameof(IOverloadResult);

        public AndConstraint<OverloadResultAssertions> HaveName(string name, string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(string.Equals(Subject.Name, name, StringComparison.Ordinal))
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {_name} to have name {name}{{reason}}.");

            return new AndConstraint<OverloadResultAssertions>(this);
        }

        public AndConstraint<OverloadResultAssertions> HaveDocumentation(string documentation, string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(string.Equals(Subject.Documentation, documentation, StringComparison.Ordinal))
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {_name} to have documentation '{documentation}'{{reason}}, but it has '{Subject.Documentation}'.");

            return new AndConstraint<OverloadResultAssertions>(this);
        }

        public AndWhichConstraint<OverloadResultAssertions, ParameterResult> HaveParameterAt(int index, string because = "", params object[] reasonArgs) {
            var parameters = Subject.Parameters;
            Execute.Assertion.ForCondition(parameters.Length > index)
                .BecauseOf(because, reasonArgs)
                .FailWith(parameters.Length > 0
                    ? $"Expected {_name} to have parameter at index {index}{{reason}}, but it has only {parameters.Length} parameters."
                    : $"Expected {_name} to have parameter at index {index}{{reason}}, but it has none.");

            return new AndWhichConstraint<OverloadResultAssertions, ParameterResult>(this, Subject.Parameters[index]);
        }

        public AndWhichConstraint<OverloadResultAssertions, ParameterResult> HaveSingleParameter(string because = "", params object[] reasonArgs) {
            var parameters = Subject.Parameters;
            Execute.Assertion.ForCondition(parameters.Length == 1)
                .BecauseOf(because, reasonArgs)
                .FailWith(parameters.Length > 0
                    ? $"Expected {_name} overload to have only one parameter{{reason}}, but it has {parameters.Length} parameters."
                    : $"Expected {_name} overload to have one parameter{{reason}}, but it has none.");

            return new AndWhichConstraint<OverloadResultAssertions, ParameterResult>(this, parameters[0]);
        }

        public AndConstraint<OverloadResultAssertions> HaveParameters(params string[] parameters) => HaveParameters(parameters, string.Empty);

        public AndConstraint<OverloadResultAssertions> HaveParameters(IEnumerable<string> parameters, string because = "", params object[] reasonArgs) {
            var current = Subject.Parameters.Select(pr => pr.Name).ToArray();
            var expected = parameters.ToArray();

            var message = GetAssertCollectionOnlyContainsMessage(current, expected, _name, "parameter", "parameters");
            Execute.Assertion.ForCondition(message == null)
                .BecauseOf(because, reasonArgs)
                .FailWith(message);

            return new AndConstraint<OverloadResultAssertions>(this);
        }

        public AndConstraint<OverloadResultAssertions> HaveNoParameters(string because = "", params object[] reasonArgs)
            => HaveParameters(Enumerable.Empty<string>(), because, reasonArgs);

        public AndConstraint<OverloadResultAssertions> HaveSingleReturnType(string type, string because = "", params object[] reasonArgs) {
            var returnTypes = ((IOverloadResult)Subject).ReturnType;
            Execute.Assertion.ForCondition(returnTypes.Count == 1)
                .BecauseOf(because, reasonArgs)
                .FailWith(returnTypes.Count > 0
                    ? $"Expected {_name} to have only one return type{{reason}}, but it has {returnTypes.Count} return types."
                    : $"Expected {_name} to have a return type{{reason}}, but it has none.");

            if (returnTypes.Count == 1) {
                Execute.Assertion.ForCondition(string.Equals(returnTypes[0], type, StringComparison.Ordinal))
                    .BecauseOf(because, reasonArgs)
                    .FailWith($"Expected {_name} to have return type [{type}]{{reason}}, but it has [{returnTypes[0]}].");
            }

            return new AndConstraint<OverloadResultAssertions>(this);
        }
    }
}