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

using System.Diagnostics.CodeAnalysis;
using FluentAssertions;

namespace Microsoft.PythonTools.Analysis.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal static class ScopeAssertionsExtensions {
        public static AndWhichConstraint<TAssertion, IFunctionScope> WithFunction<TAssertion, TScope>(this AndWhichConstraint<TAssertion, TScope> constraint, string functionName, string because = "", params object[] reasonArgs) 
            where TScope : IScope {
            var functionScope = constraint.Which.Should().HaveFunction(functionName, because, reasonArgs).Which;
            return new AndWhichConstraint<TAssertion, IFunctionScope>(constraint.And, functionScope);
        }

        public static AndWhichConstraint<TAssertion, VariableDefTestInfo> WithVariable<TAssertion, TScope>(this AndWhichConstraint<TAssertion, TScope> constraint, string functionName, string because = "", params object[] reasonArgs) 
            where TScope : IScope {
            var functionScope = constraint.Which.Should().HaveVariable(functionName, because, reasonArgs).Which;
            return new AndWhichConstraint<TAssertion, VariableDefTestInfo>(constraint.And, functionScope);
        }

        public static AndWhichConstraint<TAssertion, VariableDefTestInfo> WithParameter<TAssertion, TScope>(this AndWhichConstraint<TAssertion, TScope> constraint, string parameterName, string because = "", params object[] reasonArgs) 
            where TScope : IFunctionScope {
            var variableDefinition = constraint.Which.Should().HaveParameter(parameterName, because, reasonArgs).Which;
            return new AndWhichConstraint<TAssertion, VariableDefTestInfo>(constraint.And, variableDefinition);
        }
    }
}
