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
using Microsoft.Python.Analysis.Dependencies;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core.Text;

namespace Microsoft.Python.Analysis.Tests.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal static class AssertionsFactory {
        public static DependencyChainNodeAssertions Should(this IDependencyChainNode node) => new DependencyChainNodeAssertions(node);

        public static MemberAssertions Should(this IMember member) => new MemberAssertions(member);
        public static PythonFunctionAssertions Should(this IPythonFunctionType f) => new PythonFunctionAssertions(f);
        public static PythonFunctionOverloadAssertions Should(this IPythonFunctionOverload f) => new PythonFunctionOverloadAssertions(f);
        public static ParameterAssertions Should(this IParameterInfo p) => new ParameterAssertions(p);

        public static DocumentAnalysisAssertions Should(this IDocumentAnalysis analysis) => new DocumentAnalysisAssertions(analysis);
        public static VariableAssertions Should(this IVariable v) => new VariableAssertions(v);

        public static RangeAssertions Should(this Range? range) => new RangeAssertions(range);

        public static ScopeAssertions Should(this IScope scope) => new ScopeAssertions(scope);
        public static SourceSpanAssertions Should(this SourceSpan span) => new SourceSpanAssertions(span);
        public static SourceSpanAssertions Should(this SourceSpan? span) => new SourceSpanAssertions(span.Value);
    }
}
