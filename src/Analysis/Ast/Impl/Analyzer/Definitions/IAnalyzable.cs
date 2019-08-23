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
using Microsoft.Python.Analysis.Dependencies;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    /// <summary>
    /// Represents document that can be analyzed asynchronously.
    /// </summary>
    internal interface IAnalyzable: IDependencyProvider {
        /// <summary>
        /// Notifies document that analysis is about to begin.
        /// </summary>
        void NotifyAnalysisBegins();

        /// <summary>
        /// Performs analysis of the document base on the AST.
        /// Database-backed modules may simply do nothing.
        /// </summary>
        IDocumentAnalysis Analyze(IDependencyChainNode<PythonAnalyzerEntry> node, PythonAst ast, int version, Func<bool> isCanceled, CancellationToken cancellationToken);

        /// <summary>
        /// Notifies document that its analysis is now complete.
        /// </summary>
        /// <param name="analysis">Document analysis</param>
        void NotifyAnalysisComplete(IDocumentAnalysis analysis);
    }
}
