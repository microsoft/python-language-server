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
    }
}
