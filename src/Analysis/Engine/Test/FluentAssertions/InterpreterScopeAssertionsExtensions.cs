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
using FluentAssertions;
using Microsoft.PythonTools.Analysis.Analyzer;

namespace Microsoft.PythonTools.Analysis.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal static class InterpreterScopeAssertionsExtensions {
        public static AndWhichConstraint<TAssertion, FunctionScope> WithFunction<TAssertion, TScope>(this AndWhichConstraint<TAssertion, TScope> constraint, string functionName, string because = "", params object[] reasonArgs) 
            where TScope : InterpreterScope {
            var functionScope = constraint.Which.Should().HaveFunction(functionName, because, reasonArgs).Which;
            return new AndWhichConstraint<TAssertion, FunctionScope>(constraint.And, functionScope);
        }
    }
}
