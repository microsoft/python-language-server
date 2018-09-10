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
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal sealed class ScopeAssertions : ScopeAssertions<IScope, ScopeAssertions> {
        public ScopeAssertions(IScope scope) : base(scope) { }
    }

    [ExcludeFromCodeCoverage]
    internal class ScopeAssertions<TScope, TScopeAssertions> : ReferenceTypeAssertions<TScope, TScopeAssertions>
        where TScope : IScope
        where TScopeAssertions : ScopeAssertions<TScope, TScopeAssertions> {

        public ScopeAssertions(TScope scope) {
            Subject = scope;
        }

        protected override string Identifier => nameof(IScope);

        public AndWhichConstraint<TScopeAssertions, IPythonModule> HavePythonModuleVariable(string name, string because = "", params object[] reasonArgs) {
            var assertion = HaveVariable(name, because, reasonArgs)
                .Which.Should().HaveValue<BuiltinModule>()
                .Which.Should().HavePythonModule();

            return new AndWhichConstraint<TScopeAssertions, IPythonModule>((TScopeAssertions)this, assertion.Which);
        }

        public AndWhichConstraint<TScopeAssertions, IClassScope> HaveClass(string name, string because = "", params object[] reasonArgs) {
            var assertion = HaveVariable(name, because, reasonArgs)
                .Which.Should().HaveValue<IClassInfo>()
                .Which.Should().HaveScope();

            return new AndWhichConstraint<TScopeAssertions, IClassScope>((TScopeAssertions)this, assertion.Which);
        }

        public AndWhichConstraint<TScopeAssertions, AnalysisValueTestInfo<IClassInfo>> HaveClassInfo(string name, string because = "", params object[] reasonArgs) {
            var assertion = HaveVariable(name, because, reasonArgs)
                .Which.Should().HaveValue<IClassInfo>();

            return new AndWhichConstraint<TScopeAssertions, AnalysisValueTestInfo<IClassInfo>>((TScopeAssertions)this, assertion.Which);
        }

        public AndWhichConstraint<TScopeAssertions, OverloadResultTestInfo> HaveFunctionWithSingleOverload(string name, string because = "", params object[] reasonArgs) {
            var assertion = HaveVariable(name, because, reasonArgs)
                .Which.Should().HaveValue<IFunctionInfo>()
                .Which.Should().HaveSingleOverload(because, reasonArgs);

            return new AndWhichConstraint<TScopeAssertions, OverloadResultTestInfo>((TScopeAssertions)this, assertion.Which);
        }

        public AndWhichConstraint<TScopeAssertions, AnalysisValueTestInfo<IFunctionInfo>> HaveFunctionInfo(string name, string because = "", params object[] reasonArgs) {
            var assertion = HaveVariable(name, because, reasonArgs)
                .Which.Should().HaveValue<IFunctionInfo>();

            return new AndWhichConstraint<TScopeAssertions, AnalysisValueTestInfo<IFunctionInfo>>((TScopeAssertions)this, assertion.Which);
        }

        public AndWhichConstraint<TScopeAssertions, IFunctionScope> HaveFunction(string name, string because = "", params object[] reasonArgs) {
            var assertion = HaveVariable(name, because, reasonArgs)
                .Which.Should().HaveValue<IFunctionInfo>()
                .Which.Should().HaveFunctionScope();

            return new AndWhichConstraint<TScopeAssertions, IFunctionScope>((TScopeAssertions)this, assertion.Which);
        }
        
        public AndWhichConstraint<TScopeAssertions, VariableDefTestInfo> HaveVariable(string name, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);

            Execute.Assertion.ForCondition(Subject.TryGetVariable(name, out var variableDef))
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected scope '{Subject.Name}' to have variable '{name}'{{reason}}.");

            return new AndWhichConstraint<TScopeAssertions, VariableDefTestInfo>((TScopeAssertions)this, new VariableDefTestInfo(variableDef, name, Subject));
        }
        
        public AndConstraint<TScopeAssertions> HaveClassVariables(params string[] classNames)
            => HaveClassVariables(classNames, string.Empty);

        public AndConstraint<TScopeAssertions> HaveClassVariables(IEnumerable<string> classNames, string because = "", params object[] reasonArgs) {
            NotBeNull();

            foreach (var className in classNames) {
                HaveVariable(className, because, reasonArgs).Which.Should().HaveMemberType(PythonMemberType.Class, because, reasonArgs);
            }

            return new AndConstraint<TScopeAssertions>((TScopeAssertions)this);
        }

        public AndConstraint<TScopeAssertions> HaveFunctionVariables(params string[] functionNames) 
            => HaveFunctionVariables(functionNames, string.Empty);

        public AndConstraint<TScopeAssertions> HaveFunctionVariables(IEnumerable<string> functionNames, string because = "", params object[] reasonArgs) {
            Subject.Should().NotBeNull();

            foreach (var functionName in functionNames) {
                HaveVariable(functionName, because, reasonArgs).Which.Should().HaveMemberType(PythonMemberType.Function, because, reasonArgs);
            }

            return new AndConstraint<TScopeAssertions>((TScopeAssertions)this);
        }

        public AndConstraint<TScopeAssertions> NotHaveVariable(string name, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);

            Execute.Assertion.ForCondition(!Subject.TryGetVariable(name, out _))
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected scope '{Subject.Name}' to have no variable '{name}'{{reason}}.");

            return new AndConstraint<TScopeAssertions>((TScopeAssertions)this);
        }
    }
}