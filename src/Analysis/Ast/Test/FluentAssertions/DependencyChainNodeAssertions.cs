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
using Microsoft.Python.Analysis.Dependencies;
using static Microsoft.Python.Analysis.Tests.FluentAssertions.AssertionsUtilities;

namespace Microsoft.Python.Analysis.Tests.FluentAssertions {
    internal sealed class DependencyChainNodeAssertions : ReferenceTypeAssertions<IDependencyChainNode, DependencyChainNodeAssertions> {
        public DependencyChainNodeAssertions(IDependencyChainNode node) {
            Subject = node;
        }

        protected override string Identifier => nameof(IDependencyChainNode);
        
        [CustomAssertion]
        public AndConstraint<DependencyChainNodeAssertions> HaveSingleValue<T>(T value, string because = "", params object[] reasonArgs) {
            var currentStateMessage = Subject == null ? "null" : "loop node";
            
            Execute.Assertion.ForCondition(Subject is IDependencyChainSingleNode<T>)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected [{typeof(T)}] node to be single node{{reason}}, but it is {currentStateMessage}");

            var actual = ((IDependencyChainSingleNode<T>)Subject).Value;
            Execute.Assertion.ForCondition(Equals(actual, value))
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected [{typeof(T)}] node to have value {value}{{reason}}, but it has {actual}");

            return new AndConstraint<DependencyChainNodeAssertions>(this);
        }

        [CustomAssertion]
        public AndConstraint<DependencyChainNodeAssertions> HaveLoopValues<T>(params T[] values) => HaveLoopValues(values, string.Empty);
        
        [CustomAssertion]
        public AndConstraint<DependencyChainNodeAssertions> HaveLoopValues<T>(T[] values, string because = "", params object[] reasonArgs) {
            var currentStateMessage = Subject == null ? "null" : "loop node";
            
            Execute.Assertion.ForCondition(Subject is IDependencyChainLoopNode<T>)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected [{typeof(T)}] node to be loop node{{reason}}, but it is {currentStateMessage}");

            var actual = ((IDependencyChainLoopNode<T>)Subject).Values.ToArray();
            var errorMessage = GetAssertCollectionOnlyContainsMessage(actual, values, "loop node", "value", "values");

            Execute.Assertion.ForCondition(errorMessage == null)
                .BecauseOf(string.Empty, string.Empty)
                .FailWith(errorMessage);
            
            return new AndConstraint<DependencyChainNodeAssertions>(this);
        }

        [CustomAssertion]
        public AndConstraint<DependencyChainNodeAssertions> HaveOnlyWalkedDependencies(string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(Subject.HasOnlyWalkedDependencies)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {Subject} to have only walked dependencies{{reason}}");
            
            return new AndConstraint<DependencyChainNodeAssertions>(this);
        }

        [CustomAssertion]
        public AndConstraint<DependencyChainNodeAssertions> HaveNonWalkedDependencies(string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(!Subject.HasOnlyWalkedDependencies)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {Subject} to have non-walked dependencies{{reason}}");
            
            return new AndConstraint<DependencyChainNodeAssertions>(this);
        }

        [CustomAssertion]
        public AndConstraint<DependencyChainNodeAssertions> BeWalkedWithDependencies(string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(Subject.IsWalkedWithDependencies)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {Subject} to be walked with dependencies{{reason}}");
            
            return new AndConstraint<DependencyChainNodeAssertions>(this);
        }

        [CustomAssertion]
        public AndConstraint<DependencyChainNodeAssertions> NotBeWalkedWithDependencies(string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(!Subject.IsWalkedWithDependencies)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {Subject} to not be walked with dependencies{{reason}}");
            
            return new AndConstraint<DependencyChainNodeAssertions>(this);
        }

        public AndConstraint<DependencyChainNodeAssertions> HaveMissingDependencies(string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(Subject.HasMissingDependencies)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {Subject} to have missing dependencies{{reason}}");
            
            return new AndConstraint<DependencyChainNodeAssertions>(this);
        }

        public AndConstraint<DependencyChainNodeAssertions> HaveNoMissingDependencies(string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(!Subject.HasMissingDependencies)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {Subject} to have no missing dependencies{{reason}}");
            
            return new AndConstraint<DependencyChainNodeAssertions>(this);
        }

        public AndConstraint<DependencyChainNodeAssertions> HaveValidVersion(string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(Subject.IsValidVersion)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {Subject} to have valid version{{reason}}");
            
            return new AndConstraint<DependencyChainNodeAssertions>(this);
        }

        public AndConstraint<DependencyChainNodeAssertions> HaveInvalidVersion(string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(!Subject.IsValidVersion)
                .BecauseOf(because, reasonArgs)
                .FailWith($"Expected {Subject} to have invalid version{{reason}}");
            
            return new AndConstraint<DependencyChainNodeAssertions>(this);
        }
    }
}
