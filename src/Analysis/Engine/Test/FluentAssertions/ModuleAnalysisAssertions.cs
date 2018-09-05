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
using FluentAssertions;
using FluentAssertions.Primitives;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal sealed class ModuleAnalysisAssertions : ReferenceTypeAssertions<ModuleAnalysis, ModuleAnalysisAssertions> {
        private readonly InterpreterScopeAssertions _interpreterScopeAssertions;

        public ModuleAnalysisAssertions(ModuleAnalysis moduleAnalysis) {
            Subject = moduleAnalysis;
            _interpreterScopeAssertions = new InterpreterScopeAssertions(Subject.Scope);
        }

        protected override string Identifier => nameof(ModuleAnalysis);
        
        public AndWhichConstraint<ModuleAnalysisAssertions, IPythonModule> HavePythonModuleVariable(string name, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);
            var constraint = _interpreterScopeAssertions.HavePythonModuleVariable(name, because, reasonArgs);
            return new AndWhichConstraint<ModuleAnalysisAssertions, IPythonModule>(this, constraint.Which);
        }

        public AndWhichConstraint<ModuleAnalysisAssertions, AnalysisValueTestInfo<ClassInfo>> HaveClassInfo(string name, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);
            var constraint = _interpreterScopeAssertions.HaveClassInfo(name, because, reasonArgs);
            return new AndWhichConstraint<ModuleAnalysisAssertions, AnalysisValueTestInfo<ClassInfo>>(this, constraint.Which);
        }

        public AndWhichConstraint<ModuleAnalysisAssertions, ClassScope> HaveClass(string name, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);
            var constraint = _interpreterScopeAssertions.HaveClass(name, because, reasonArgs);
            return new AndWhichConstraint<ModuleAnalysisAssertions, ClassScope>(this, constraint.Which);
        }
        
        public AndWhichConstraint<ModuleAnalysisAssertions, OverloadResultTestInfo> HaveFunctionWithSingleOverload(string name, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);
            var constraint = _interpreterScopeAssertions.HaveFunctionWithSingleOverload(name, because, reasonArgs);
            return new AndWhichConstraint<ModuleAnalysisAssertions, OverloadResultTestInfo>(this, constraint.Which);
        }

        public AndWhichConstraint<ModuleAnalysisAssertions, AnalysisValueTestInfo<FunctionInfo>> HaveFunctionInfo(string name, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);
            var constraint = _interpreterScopeAssertions.HaveFunctionInfo(name, because, reasonArgs);
            return new AndWhichConstraint<ModuleAnalysisAssertions, AnalysisValueTestInfo<FunctionInfo>>(this, constraint.Which);
        }

        public AndWhichConstraint<ModuleAnalysisAssertions, FunctionScope> HaveFunction(string name, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);
            var constraint = _interpreterScopeAssertions.HaveFunction(name, because, reasonArgs);
            return new AndWhichConstraint<ModuleAnalysisAssertions, FunctionScope>(this, constraint.Which);
        }

        public AndWhichConstraint<ModuleAnalysisAssertions, VariableDefTestInfo> HaveVariable(string name, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);
            var constraint = _interpreterScopeAssertions.HaveVariable(name, because, reasonArgs);
            return new AndWhichConstraint<ModuleAnalysisAssertions, VariableDefTestInfo>(this, constraint.Which);
        }

        public AndConstraint<ModuleAnalysisAssertions> HaveClassVariables(params string[] classNames)
            => HaveClassVariables(classNames, string.Empty);

        public AndConstraint<ModuleAnalysisAssertions> HaveClassVariables(IEnumerable<string> classNames, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);
            _interpreterScopeAssertions.HaveClassVariables(classNames, because, reasonArgs);
            return new AndConstraint<ModuleAnalysisAssertions>(this);
        }

        public AndConstraint<ModuleAnalysisAssertions> HaveFunctionVariables(params string[] functionNames) 
            => HaveFunctionVariables(functionNames, string.Empty);

        public AndConstraint<ModuleAnalysisAssertions> HaveFunctionVariables(IEnumerable<string> functionNames, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);
            _interpreterScopeAssertions.HaveFunctionVariables(functionNames, because, reasonArgs);
            return new AndConstraint<ModuleAnalysisAssertions>(this);
        }

        public AndConstraint<ModuleAnalysisAssertions> NotHaveVariable(string name, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);
            _interpreterScopeAssertions.NotHaveVariable(name, because, reasonArgs);
            return new AndConstraint<ModuleAnalysisAssertions>(this);
        }
    }
}