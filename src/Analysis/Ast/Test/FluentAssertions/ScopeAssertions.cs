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

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Tests.FluentAssertions {
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

        [CustomAssertion]
        public AndWhichConstraint<TScopeAssertions, TChildScope> OnlyHaveChildScope<TChildScope>(string because = "", params object[] reasonArgs)
            where TChildScope : IScope => HaveChildScopeAt<TChildScope>(0, because, reasonArgs);

        [CustomAssertion]
        public AndWhichConstraint<TScopeAssertions, TChildScope> HaveChildScopeAt<TChildScope>(int index, string because = "", params object[] reasonArgs)
            where TChildScope : IScope {
            NotBeNull(because, reasonArgs);
            var childScopes = Subject.Children;
            var subjectName = $"scope '{Subject.Name}'";
            Execute.Assertion.BecauseOf(because, reasonArgs)
                .AssertIsNotNull(childScopes, subjectName, $"child scope of type '{typeof(TScope).Name}'", $"'{Subject.Name}.Children'")
                .Then
                .AssertAtIndex<IScope, TChildScope>(childScopes, index, subjectName, "child scope");

            return new AndWhichConstraint<TScopeAssertions, TChildScope>((TScopeAssertions)this, (TChildScope)childScopes[index]);
        }

        public AndWhichConstraint<TScopeAssertions, IPythonClassType> HaveClass(string name, string because = "", params object[] reasonArgs) {
            var v = HaveVariable(name, because, reasonArgs).Which;
            v.Value.Should().BeAssignableTo<IPythonClassType>();

            return new AndWhichConstraint<TScopeAssertions, IPythonClassType>((TScopeAssertions)this, (IPythonClassType)v.Value);
        }

        public AndWhichConstraint<TScopeAssertions, IPythonFunctionType> HaveFunction(string name, string because = "", params object[] reasonArgs) {
            var f = HaveVariable(name, because, reasonArgs).Which;
            f.Value.Should().BeAssignableTo<IPythonFunctionType>();

            return new AndWhichConstraint<TScopeAssertions, IPythonFunctionType>((TScopeAssertions)this, (IPythonFunctionType)f.Value);
        }

        public AndWhichConstraint<TScopeAssertions, IVariable> HaveVariable(string name, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);

            var v = Subject.Variables[name];
            Execute.Assertion.ForCondition(v != null)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected scope '{Subject.Name}' to have variable '{name}'{{reason}}.");

            return new AndWhichConstraint<TScopeAssertions, IVariable>((TScopeAssertions)this, v);
        }

        public AndWhichConstraint<TScopeAssertions, IVariable> HaveGenericVariable(string name, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);

            var v = Subject.Variables[name];
            Execute.Assertion.ForCondition(v != null && v.IsGeneric())
                           .BecauseOf(because, reasonArgs)
                           .FailWith($"Expected scope '{Subject.Name}' to have generic variable '{name}'{{reason}}.");

            return new AndWhichConstraint<TScopeAssertions, IVariable>((TScopeAssertions)this, v);
        }

        public AndConstraint<TScopeAssertions> HaveClassVariables(params string[] classNames)
            => HaveClassVariables(classNames, string.Empty);

        public AndConstraint<TScopeAssertions> HaveClassVariables(IEnumerable<string> classNames, string because = "", params object[] reasonArgs) {
            NotBeNull();

            foreach (var className in classNames) {
                HaveVariable(className, because, reasonArgs).OfType(className, because, reasonArgs);
            }

            return new AndConstraint<TScopeAssertions>((TScopeAssertions)this);
        }

        public AndConstraint<TScopeAssertions> HaveFunctionVariables(params string[] functionNames)
            => HaveFunctionVariables(functionNames, string.Empty);

        public AndConstraint<TScopeAssertions> HaveFunctionVariables(IEnumerable<string> functionNames, string because = "", params object[] reasonArgs) {
            Subject.Should().NotBeNull();

            foreach (var functionName in functionNames) {
                HaveVariable(functionName, because, reasonArgs).OfType(functionName, because, reasonArgs);
            }

            return new AndConstraint<TScopeAssertions>((TScopeAssertions)this);
        }

        public AndConstraint<TScopeAssertions> NotHaveVariable(string name, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);

            Execute.Assertion.ForCondition(Subject.Variables[name] == null)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected scope '{Subject.Name}' to have no variable '{name}'{{reason}}.");

            return new AndConstraint<TScopeAssertions>((TScopeAssertions)this);
        }
    }
}
