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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Ast;

namespace Microsoft.PythonTools.Analysis.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal class AstPythonFunctionAssertions : ReferenceTypeAssertions<AstPythonFunction, AstPythonFunctionAssertions> {
        public AstPythonFunctionAssertions(AstPythonFunction pythonFunction) {
            Subject = pythonFunction;
        }

        protected override string Identifier => nameof(AstPythonFunction);

        public AndConstraint<AstPythonFunctionAssertions> BeClassMethod(string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(Subject.IsClassMethod)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {Subject.FullyQualifiedName} to be a class method{{reason}}");

            return new AndConstraint<AstPythonFunctionAssertions>(this);
        }

        public AndWhichConstraint<AstPythonFunctionAssertions, PythonFunctionOverloadTestInfo> HaveSingleOverload(string because = "", params object[] reasonArgs) {
            var overloads = Subject.Overloads.ToArray();
            Execute.Assertion.ForCondition(overloads.Length == 1)
                .BecauseOf(because, reasonArgs)
                .FailWith(overloads.Length > 0
                    ? $"Expected {Subject.FullyQualifiedName} to have only one overload{{reason}}, but it has {overloads.Length} overloads."
                    : $"Expected {Subject.FullyQualifiedName} to have an overload{{reason}}, but it has none.");

            return new AndWhichConstraint<AstPythonFunctionAssertions, PythonFunctionOverloadTestInfo>(this, new PythonFunctionOverloadTestInfo(overloads[0], Subject.FullyQualifiedName));
        }

        public AndWhichConstraint<AstPythonFunctionAssertions, PythonFunctionOverloadTestInfo> HaveOverloadAt(int index, string because = "", params object[] reasonArgs) {
            var overloads = Subject.Overloads.ToArray();
            Execute.Assertion.ForCondition(overloads.Length > index)
                .BecauseOf(because, reasonArgs)
                .FailWith(overloads.Length > 0
                    ? $"Expected {Subject.FullyQualifiedName} to have overload at index '{index}'{{reason}}, but it has only {overloads.Length} overloads."
                    : $"Expected {Subject.FullyQualifiedName} to have overload at index '{index}'{{reason}}, but it has none.");

            return new AndWhichConstraint<AstPythonFunctionAssertions, PythonFunctionOverloadTestInfo>(this, new PythonFunctionOverloadTestInfo(overloads[index], Subject.FullyQualifiedName));
        }
    }
}