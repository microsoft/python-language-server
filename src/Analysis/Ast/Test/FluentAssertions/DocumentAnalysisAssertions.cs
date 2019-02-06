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
using FluentAssertions.Primitives;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Tests.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal sealed class DocumentAnalysisAssertions : ReferenceTypeAssertions<IDocumentAnalysis, DocumentAnalysisAssertions> {
        private readonly ScopeAssertions _scopeAssertions;

        public DocumentAnalysisAssertions(IDocumentAnalysis analysis) {
            Subject = analysis;
            _scopeAssertions = new ScopeAssertions(Subject.GlobalScope);
        }

        protected override string Identifier => nameof(IDocumentAnalysis);

        public AndWhichConstraint<DocumentAnalysisAssertions, IPythonFunctionType> HaveFunction(string name, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);
            var constraint = _scopeAssertions.HaveFunction(name, because, reasonArgs);
            return new AndWhichConstraint<DocumentAnalysisAssertions, IPythonFunctionType>(this, constraint.Which);
        }

        public AndWhichConstraint<DocumentAnalysisAssertions, IPythonClassType> HaveClass(string name, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);
            var constraint = _scopeAssertions.HaveClass(name, because, reasonArgs);
            return new AndWhichConstraint<DocumentAnalysisAssertions, IPythonClassType>(this, constraint.Which);
        }

        public AndWhichConstraint<DocumentAnalysisAssertions, IVariable> HaveVariable(string name, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);
            var constraint = _scopeAssertions.HaveVariable(name, because, reasonArgs);
            return new AndWhichConstraint<DocumentAnalysisAssertions, IVariable>(this, constraint.Which);
        }

        public AndConstraint<DocumentAnalysisAssertions> HaveClassVariables(params string[] classNames)
            => HaveClassVariables(classNames, string.Empty);

        public AndConstraint<DocumentAnalysisAssertions> HaveClassVariables(IEnumerable<string> classNames, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);
            _scopeAssertions.HaveClassVariables(classNames, because, reasonArgs);
            return new AndConstraint<DocumentAnalysisAssertions>(this);
        }

        public AndConstraint<DocumentAnalysisAssertions> HaveFunctionVariables(params string[] functionNames) 
            => HaveFunctionVariables(functionNames, string.Empty);

        public AndConstraint<DocumentAnalysisAssertions> HaveFunctionVariables(IEnumerable<string> functionNames, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);
            _scopeAssertions.HaveFunctionVariables(functionNames, because, reasonArgs);
            return new AndConstraint<DocumentAnalysisAssertions>(this);
        }

        public AndConstraint<DocumentAnalysisAssertions> NotHaveVariable(string name, string because = "", params object[] reasonArgs) {
            NotBeNull(because, reasonArgs);
            _scopeAssertions.NotHaveVariable(name, because, reasonArgs);
            return new AndConstraint<DocumentAnalysisAssertions>(this);
        }
    }
}
