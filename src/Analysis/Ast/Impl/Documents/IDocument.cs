﻿// Copyright(c) Microsoft Corporation
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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Documents {
    /// <summary>
    /// Represent document (file) loaded for the analysis.
    /// </summary>
    public interface IDocument: IDisposable {
        /// <summary>
        /// File path to the module.
        /// </summary>
        string FilePath { get; }

        /// <summary>
        /// Module name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Module URI.
        /// </summary>
        Uri Uri { get; }

        /// <summary>
        /// Module content version (increments after every change).
        /// </summary>
        int Version { get; }

        /// <summary>
        /// Indicates if module belongs to the workspace tree.
        /// </summary>
        bool IsInWorkspace { get; }

        /// <summary>
        /// Indicates if module is open in the editor.
        /// </summary>
        bool IsOpen { get; }

        /// <summary>
        /// Document parse tree
        /// </summary>
        Task<PythonAst> GetAstAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Python module type.
        /// </summary>
        IPythonModule PythonModule { get; }

        /// <summary>
        /// Returns reader to read the document content.
        /// </summary>
        TextReader GetReader();

        /// <summary>
        /// Returns document content as stream.
        /// </summary>
        Stream GetStream();

        /// <summary>
        /// Returns document content as string.
        /// </summary>
        string GetContent();

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
