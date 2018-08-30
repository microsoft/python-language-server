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

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using static Microsoft.PythonTools.Analysis.FluentAssertions.AssertionsUtilities;

namespace Microsoft.PythonTools.Analysis.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal sealed class FunctionScopeAssertions : InterpreterScopeAssertions<FunctionScope, FunctionScopeAssertions> {
        public FunctionScopeAssertions(FunctionScope interpreterScope) : base(interpreterScope) { }

        public AndWhichConstraint<FunctionScopeAssertions, VariableDefTestInfo> HaveParameter(string name, string because = "", params object[] reasonArgs) {
            NotBeNull();

            var parameter = Subject.GetParameter(name);
            Execute.Assertion.ForCondition(parameter != null)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected function '{Subject.Name}' to have parameter '{name}'{{reason}}.");

            return new AndWhichConstraint<FunctionScopeAssertions, VariableDefTestInfo>(this, new VariableDefTestInfo(parameter, name, Subject));
        }

        public AndWhichConstraint<FunctionScopeAssertions, VariableDefTestInfo> HaveReturnValue(string because = "", params object[] reasonArgs) {
            NotBeNull();

            Execute.Assertion.ForCondition(Subject.ReturnValue != null)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected function '{GetQuotedName(Subject.Function)}' to have return value {{reason}}.");

            return new AndWhichConstraint<FunctionScopeAssertions, VariableDefTestInfo>(this, new VariableDefTestInfo(Subject.ReturnValue, "<return value>", Subject));
        }

        
        public AndConstraint<FunctionScopeAssertions> HaveResolvedReturnTypes(params BuiltinTypeId[] typeIds) 
            => HaveResolvedReturnTypes(typeIds, string.Empty);

        public AndConstraint<FunctionScopeAssertions> HaveResolvedReturnTypes(IEnumerable<BuiltinTypeId> typeIds, string because = "", params object[] reasonArgs) {
            var is3X = Subject.Function.ProjectEntry.ProjectState.LanguageVersion.Is3x();
            var analysisUnit = Subject.Function.AnalysisUnit;
            var actualTypeIds = Subject.ReturnValue.TypesNoCopy.Resolve(analysisUnit).Select(v => v.TypeId);
            var name = $"function {GetQuotedName(Subject.Function)} return value";

            AssertTypeIds(actualTypeIds, typeIds, name, is3X, because, reasonArgs);
            
            return new AndConstraint<FunctionScopeAssertions>(this);
        }

    }
}