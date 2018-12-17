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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Documents {
    /// <summary>
    /// Represent document (file) loaded for the analysis.
    /// </summary>
    public interface IDocument: IPythonModule {
        /// <summary>
        /// Module content version (increments after every change).
        /// </summary>
        int Version { get; }

        /// <summary>
        /// Indicates that the document is open in the editor.
        /// </summary>
        bool IsOpen { get; set; }

        /// <summary>
        /// Returns module content (code).
        /// </summary>
        string Content { get; }

        /// <summary>
        /// Returns document parse tree.
        /// </summary>
        Task<PythonAst> GetAstAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns document analysis.
        /// </summary>
        Task<IDocumentAnalysis> GetAnalysisAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns last known document analysis. The analysis may be out of date.
        /// </summary>
        IDocumentAnalysis GetAnyAnalysis();

        /// <summary>
        /// Updates document content with the list of changes.
        /// </summary>
        /// <param name="changes"></param>
        void Update(IEnumerable<DocumentChangeSet> changes);

        /// <summary>
        /// Provides collection of parsing errors, if any.
        /// </summary>
        IEnumerable<DiagnosticsEntry> GetDiagnostics();

        /// <summary>
        /// Fires when new AST is ready (typically as a result of the document change)
        /// </summary>
        event EventHandler<EventArgs> NewAst;

        /// <summary>
        /// Fires when new analysis is ready (typically as a result of the document change)
        /// </summary>
        event EventHandler<EventArgs> NewAnalysis;
    }
}
