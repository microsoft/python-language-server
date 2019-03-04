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

using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;
using ErrorCodes = Microsoft.Python.Analysis.Diagnostics.ErrorCodes;

namespace Microsoft.Python.Analysis.Linting.UndefinedVariables {
    internal static class AnalysisExtensions {
        public static void ReportUndefinedVariable(this IDocumentAnalysis analysis, NameExpression node) {
            var eval = analysis.ExpressionEvaluator;
            eval.ReportDiagnostics(analysis.Document.Uri, new DiagnosticsEntry(
                Resources.UndefinedVariable.FormatInvariant(node.Name),
                eval.GetLocation(node).Span, ErrorCodes.UndefinedVariable, Severity.Warning));
        }
    }
}
