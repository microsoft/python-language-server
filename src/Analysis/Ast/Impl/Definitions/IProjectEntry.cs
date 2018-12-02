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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis {
    /// <summary>
    /// Represents a file which is capable of being analyzed.
    /// </summary>
    public interface IProjectEntry : IDisposable {
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
        /// Document object corresponding to the entry.
        /// Can be null for entries that are not user documents
        /// such as modules.
        /// </summary>
        IDocument Document { get; }

        /// <summary>
        /// Document parse tree
        /// </summary>
        Task<PythonAst> GetAst(CancellationToken cancellationToken = default);

        event EventHandler<EventArgs> NewAst;
        event EventHandler<EventArgs> NewAnalysis;
    }
}
