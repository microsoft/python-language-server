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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Python.Analysis.Analyzer.Evaluation;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    internal sealed class DocumentAnalysis : IDocumentAnalysis {
        public DocumentAnalysis(IDocument document, int version, IGlobalScope globalScope, IExpressionEvaluator eval, IReadOnlyList<string> starImportMemberNames) {
            Check.ArgumentNotNull(nameof(document), document);
            Check.ArgumentNotNull(nameof(globalScope), globalScope);
            Document = document;
            Version = version;
            GlobalScope = globalScope;
            ExpressionEvaluator = eval;
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
        /// AST that was used in the analysis.
        /// </summary>
        public PythonAst Ast => ExpressionEvaluator.Ast;

        /// <summary>
        /// Document/module global scope.
        /// </summary>
        public IGlobalScope GlobalScope { get; }

        /// <summary>
        /// ValueExpression evaluator used in the analysis.
        /// </summary>
        public IExpressionEvaluator ExpressionEvaluator { get; }

        /// <summary>
        /// Members of the module which are transferred during a star import. null means __all__ was not defined.
        /// </summary>
        public IReadOnlyList<string> StarImportMemberNames { get; }

        /// <summary>
        /// Analysis diagnostics.
        /// </summary>
        public IEnumerable<DiagnosticsEntry> Diagnostics => ExpressionEvaluator.Diagnostics;
        #endregion
    }

    public sealed class EmptyAnalysis : IDocumentAnalysis {
        private static PythonAst _emptyAst;

        public EmptyAnalysis(IServiceContainer services, IDocument document) {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            GlobalScope = new EmptyGlobalScope(document);

            _emptyAst = _emptyAst ?? (_emptyAst = Parser.CreateParser(new StringReader(string.Empty), PythonLanguageVersion.None).ParseFile(document.Uri));
            ExpressionEvaluator = new ExpressionEval(services, document, Ast);
        }

        public IDocument Document { get; }
        public int Version { get; } = -1;
        public IGlobalScope GlobalScope { get; }
        public PythonAst Ast => _emptyAst;
        public IExpressionEvaluator ExpressionEvaluator { get; }
        public IReadOnlyList<string> StarImportMemberNames => null;
        public IEnumerable<DiagnosticsEntry> Diagnostics => Enumerable.Empty<DiagnosticsEntry>();
    }
}
