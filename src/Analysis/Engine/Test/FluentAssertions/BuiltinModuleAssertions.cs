﻿// Python Tools for Visual Studio
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
using FluentAssertions.Execution;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal sealed class BuiltinModuleAssertions : AnalysisValueAssertions<BuiltinModule, BuiltinModuleAssertions> {
        public BuiltinModuleAssertions(AnalysisValueTestInfo<BuiltinModule> subject) : base(subject) {}

        protected override string Identifier => nameof(BuiltinModule);

        [CustomAssertion]
        public AndWhichConstraint<BuiltinModuleAssertions, IPythonModule> HavePythonModule(string because = "", params object[] reasonArgs) {
            Execute.Assertion.ForCondition(Subject.InterpreterModule != null)
                .BecauseOf(because, reasonArgs)
                .FailWith($@"Expected BuiltinModule '{OwnerScope.Name}.{Subject.Name}' to have InterpreterModule{{reason}}.");

            return new AndWhichConstraint<BuiltinModuleAssertions, IPythonModule>(this, Subject.InterpreterModule);
        }
    }

    [ExcludeFromCodeCoverage]
    internal sealed class PythonPackageAssertions : AnalysisValueAssertions<PythonPackage, PythonPackageAssertions> {
        public PythonPackageAssertions(AnalysisValueTestInfo<PythonPackage> subject) : base(subject) {}

        protected override string Identifier => nameof(PythonPackage);

        [CustomAssertion]
        public AndWhichConstraint<PythonPackageAssertions, IPythonModule> HaveChildModule(string name, string because = "", params object[] reasonArgs) {
            var module = Subject.GetChildPackage(null, name);
            var builtinModule = module as BuiltinModule;
            Execute.Assertion.BecauseOf(because, reasonArgs)
                .ForCondition(module != null)
                .FailWith($"Expected package {Subject.Name} to have value child module {name}{{reason}}")
                .Then
                .ForCondition(builtinModule != null)
                .FailWith($"Expected package {Subject.Name} to have value child module {name} of type {typeof(BuiltinModule)}{{reason}}, but its value has type {module.GetType()}.")
                .Then
                .ForCondition(builtinModule.InterpreterModule != null)
                .FailWith($@"Expected BuiltinModule '{OwnerScope.Name}.{Subject.Name}' to have InterpreterModule{{reason}}.");

            return new AndWhichConstraint<PythonPackageAssertions, IPythonModule>(this, builtinModule.InterpreterModule);
        }
    }
}
