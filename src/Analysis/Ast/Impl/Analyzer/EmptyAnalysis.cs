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
using System.Linq;
using Microsoft.Python.Analysis.Analyzer.Evaluation;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Utilities;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    public sealed class EmptyAnalysis : IDocumentAnalysis {
        public EmptyAnalysis(IServiceContainer services, IDocument document) {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            GlobalScope = new EmptyGlobalScope(document);
            Ast = AstUtilities.MakeEmptyAst(document.Uri);
            ExpressionEvaluator = new ExpressionEval(services, document, Ast);
        }

        public IDocument Document { get; }
        public int Version { get; } = -1;
        public IGlobalScope GlobalScope { get; }
        public PythonAst Ast { get; }
        public IExpressionEvaluator ExpressionEvaluator { get; }
        public IReadOnlyList<string> StarImportMemberNames => null;
        public IEnumerable<DiagnosticsEntry> Diagnostics => Enumerable.Empty<DiagnosticsEntry>();
    }
}
