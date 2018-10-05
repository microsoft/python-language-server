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

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal sealed class ModuleAnalysisAssertions : ReferenceTypeAssertions<IModuleAnalysis, ModuleAnalysisAssertions> {
        private readonly ScopeAssertions _scopeAssertions;

        public ModuleAnalysisAssertions(IModuleAnalysis moduleAnalysis) {
            Subject = moduleAnalysis;
            _scopeAssertions = new ScopeAssertions(Subject.Scope);
        }

        protected override string Identifier => nameof(IModuleAnalysis);

        public AndWhichConstraint<ModuleAnalysisAssertions, AnalysisValueTestInfo<TMember>> HaveBuiltinMember<TMember>(string name, string because = "", params object[] reasonArgs) 
            where TMember : class, IAnalysisValue {
            NotBeNull(because, reasonArgs);

            Execute.Assertion.BecauseOf(because, reasonArgs)
                .AssertIsNotNull(Subject.ProjectState, $"module '{Subject.ModuleName}'", "python analyzer", "\'IModuleAnalysis.ProjectState\'")
                .Then
                .AssertIsNotNull(Subject.ProjectState.BuiltinModule, $"module '{Subject.ModuleName}'", "builtin module", "\'IModuleAnalysis.ProjectState.BuiltinModule\'")
                .Then
                .AssertHasMemberOfType<TMember>(Subject.ProjectState.BuiltinModule, Subject.Scope, name, $"module '{Subject.ModuleName}'", $"builtin member '{name}'", out var typedMember);

            return new AndWhichConstraint<ModuleAnalysisAssertions, AnalysisValueTestInfo<TMember>>(this, new AnalysisValueTestInfo<TMember>(typedMember, null, Subject.Scope));
        }

        public AndWhichConstraint<ModuleAnalysisAssertions, IPythonModule> HavePythonModuleVariable(string name, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);
            var constraint = _scopeAssertions.HavePythonModuleVariable(name, because, reasonArgs);
            return new AndWhichConstraint<ModuleAnalysisAssertions, IPythonModule>(this, constraint.Which);
        }

        public AndWhichConstraint<ModuleAnalysisAssertions, AnalysisValueTestInfo<IClassInfo>> HaveClassInfo(string name, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);
            var constraint = _scopeAssertions.HaveClassInfo(name, because, reasonArgs);
            return new AndWhichConstraint<ModuleAnalysisAssertions, AnalysisValueTestInfo<IClassInfo>>(this, constraint.Which);
        }

        public AndWhichConstraint<ModuleAnalysisAssertions, IClassScope> HaveClass(string name, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);
            var constraint = _scopeAssertions.HaveClass(name, because, reasonArgs);
            return new AndWhichConstraint<ModuleAnalysisAssertions, IClassScope>(this, constraint.Which);
        }
        
        public AndWhichConstraint<ModuleAnalysisAssertions, OverloadResultTestInfo> HaveFunctionWithSingleOverload(string name, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);
            var constraint = _scopeAssertions.HaveFunctionWithSingleOverload(name, because, reasonArgs);
            return new AndWhichConstraint<ModuleAnalysisAssertions, OverloadResultTestInfo>(this, constraint.Which);
        }

        public AndWhichConstraint<ModuleAnalysisAssertions, AnalysisValueTestInfo<IFunctionInfo>> HaveFunctionInfo(string name, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);
            var constraint = _scopeAssertions.HaveFunctionInfo(name, because, reasonArgs);
            return new AndWhichConstraint<ModuleAnalysisAssertions, AnalysisValueTestInfo<IFunctionInfo>>(this, constraint.Which);
        }

        public AndWhichConstraint<ModuleAnalysisAssertions, IFunctionScope> HaveFunction(string name, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);
            var constraint = _scopeAssertions.HaveFunction(name, because, reasonArgs);
            return new AndWhichConstraint<ModuleAnalysisAssertions, IFunctionScope>(this, constraint.Which);
        }

        public AndWhichConstraint<ModuleAnalysisAssertions, VariableDefTestInfo> HaveVariable(string name, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);
            var constraint = _scopeAssertions.HaveVariable(name, because, reasonArgs);
            return new AndWhichConstraint<ModuleAnalysisAssertions, VariableDefTestInfo>(this, constraint.Which);
        }

        public AndConstraint<ModuleAnalysisAssertions> HaveClassVariables(params string[] classNames)
            => HaveClassVariables(classNames, string.Empty);

        public AndConstraint<ModuleAnalysisAssertions> HaveClassVariables(IEnumerable<string> classNames, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);
            _scopeAssertions.HaveClassVariables(classNames, because, reasonArgs);
            return new AndConstraint<ModuleAnalysisAssertions>(this);
        }

        public AndConstraint<ModuleAnalysisAssertions> HaveFunctionVariables(params string[] functionNames) 
            => HaveFunctionVariables(functionNames, string.Empty);

        public AndConstraint<ModuleAnalysisAssertions> HaveFunctionVariables(IEnumerable<string> functionNames, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);
            _scopeAssertions.HaveFunctionVariables(functionNames, because, reasonArgs);
            return new AndConstraint<ModuleAnalysisAssertions>(this);
        }

        public AndConstraint<ModuleAnalysisAssertions> NotHaveVariable(string name, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);
            _scopeAssertions.NotHaveVariable(name, because, reasonArgs);
            return new AndConstraint<ModuleAnalysisAssertions>(this);
        }
    }
}