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
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis {
    /// <summary>
    /// Represents analysis of the Python module.
    /// </summary>
    public interface IDocumentAnalysis {
        /// <summary>
        /// Analyzed document.
        /// </summary>
        IDocument Document { get; }

        /// <summary>
        /// Version of the analysis. Usually matches document version,
        /// but can be lower when document or its dependencies were
        /// updated since.
        /// </summary>
        int Version { get; }

        /// <summary>
        /// AST that was used in the analysis.
        /// </summary>
        PythonAst Ast { get; }

        /// <summary>
        /// Document/module global scope.
        /// </summary>
        IGlobalScope GlobalScope { get; }

        /// <summary>
        /// ValueExpression evaluator used in the analysis.
        /// </summary>
        IExpressionEvaluator ExpressionEvaluator { get; }

        /// <summary>
        /// Members of the module explicitly specified for export
        /// </summary>
        ImmutableArray<string> ExportedMemberNames { get; }

        /// <summary>
        /// Analysis diagnostics.
        /// </summary>
        IEnumerable<DiagnosticsEntry> Diagnostics { get; }
    }
}
