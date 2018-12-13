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
using Microsoft.Python.Analysis.Analyzer.Types;

namespace Microsoft.Python.Analysis.Documents {
    /// <summary>
    /// Represents collection of loaded modules.
    /// </summary>
    public interface IDocumentTable: IEnumerable<IDocument> {
        /// <summary>
        /// Adds file to the list of available documents.
        /// </summary>
        /// <param name="uri">Document URI.</param>
        /// <param name="content">Document content</param>
        IDocument AddDocument(Uri uri, string content);

        /// <summary>
        /// Adds library module to the list of available documents.
        /// </summary>
        /// <param name="moduleName">The name of the module; used to associate with imports</param>
        /// <param name="moduleType">Module type (library or stub).</param>
        /// <param name="filePath">The path to the file on disk</param>
        /// <param name="uri">Document URI. Can be null if module is not a user document.</param>
        /// <param name="options">Document creation options.</param>
        IDocument AddModule(string moduleName, ModuleType moduleType, string filePath, Uri uri, DocumentCreationOptions options);

        /// <summary>
        /// Removes document from the table.
        /// </summary>
        void RemoveDocument(Uri uri);

        /// <summary>
        /// Fetches document by its URI. Returns null if document is not loaded.
        /// </summary>
        IDocument GetDocument(Uri uri);

        /// <summary>
        /// Fetches document by name. Returns null if document is not loaded.
        /// </summary>
        IDocument GetDocument(string name);

        /// <summary>
        /// Fires when document is opened.
        /// </summary>
        event EventHandler<DocumentEventArgs> Opened;

        /// <summary>
        /// Fires when document is closed.
        /// </summary>
        event EventHandler<DocumentEventArgs> Closed;
    }
}
