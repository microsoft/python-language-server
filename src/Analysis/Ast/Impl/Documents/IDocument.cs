// Python Tools for Visual Studio
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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis {
    public interface IDocument: IDisposable {
        /// <summary>
        /// Returns the project entries file path.
        /// </summary>
        string FilePath { get; }

        /// <summary>
        /// Document URI.
        /// </summary>
        Uri Uri { get; }

        /// <summary>
        /// Module name.
        /// </summary>
        string ModuleName { get; }

        /// <summary>
        /// Document version (increments after every change).
        /// </summary>
        int Version { get; }

        /// <summary>
        /// Updates document content with the list of changes.
        /// </summary>
        /// <param name="changes"></param>
        void Update(IEnumerable<DocumentChange> changes);

        /// <summary>
        /// Document parse tree
        /// </summary>
        Task<PythonAst> GetAst(CancellationToken cancellationToken = default);

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
        /// Fires when new AST is ready (typically as a result of the document change)
        /// </summary>
        event EventHandler<EventArgs> NewAst;

        /// <summary>
        /// Fires when new analysis is ready (typically as a result of the document change)
        /// </summary>
        event EventHandler<EventArgs> NewAnalysis;
    }
}
