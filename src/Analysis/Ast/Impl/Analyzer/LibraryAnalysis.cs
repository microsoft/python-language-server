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
using System.Linq;
using Microsoft.Python.Analysis.Analyzer.Evaluation;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    /// <summary>
    /// Analysis of a library code.
    /// </summary>
    internal sealed class LibraryAnalysis : IDocumentAnalysis {
        public LibraryAnalysis(IDocument document, int version, IServiceContainer services, GlobalScope globalScope, IReadOnlyList<string> starImportMemberNames) {
            Check.ArgumentNotNull(nameof(document), document);
            Check.ArgumentNotNull(nameof(globalScope), globalScope);
            Document = document;
            Version = version;
            GlobalScope = globalScope;

            var ast = Document.GetAst();
            (ast.Body as SuiteStatement)?.FilterStatements(x => x is ImportStatement || x is FromImportStatement);
            ast.Reduce();
            var c = (IAstNodeContainer)Document;
            c.Clear();
            c.AddAstNode(document, ast);

            ExpressionEvaluator = new ExpressionEval(services, document, globalScope);
            StarImportMemberNames = starImportMemberNames;
        }

        #region IDocumentAnalysis
        /// <summary>
        /// Analyzed document.
        /// </summary>
        public IDocument Document { get; }

        /// <summary>
        /// Version of the analysis. Usually matches document version,
        /// but can be lower when document or its dependencies were
        /// updated since.
        /// </summary>
        public int Version { get; }

        /// <summary>
        /// Empty AST.
        /// </summary>
        public PythonAst Ast => ExpressionEvaluator.Ast;

        /// <summary>
        /// Document/module global scope.
        /// </summary>
        public IGlobalScope GlobalScope { get; }

        /// <summary>
        /// Expression evaluator used in the analysis.
        /// Only supports scope operation since there is no AST
        /// when library analysis is complete.
        /// </summary>
        public IExpressionEvaluator ExpressionEvaluator { get; }

        /// <summary>
        /// Members of the module which are transferred during a star import. null means __all__ was not defined.
        /// </summary>
        public IReadOnlyList<string> StarImportMemberNames { get; }

        /// <summary>
        /// Analysis diagnostics.
        /// </summary>
        public IEnumerable<DiagnosticsEntry> Diagnostics => Enumerable.Empty<DiagnosticsEntry>();
        #endregion
    }
}
