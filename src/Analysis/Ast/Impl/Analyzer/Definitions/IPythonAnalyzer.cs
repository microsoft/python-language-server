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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Documents;

namespace Microsoft.Python.Analysis.Analyzer {
    public interface IPythonAnalyzer {
        /// <summary>
        /// Returns the interpreter that the analyzer is using.
        /// This property is thread safe.
        /// </summary>
        IPythonInterpreter Interpreter { get; }

        /// <summary>
        /// Analyze single document.
        /// </summary>
        Task<IDocumentAnalysis> AnalyzeDocumentAsync(IDocument document, CancellationToken cancellationToken);

        /// <summary>
        /// Analyze document with dependents.
        /// </summary>
        Task AnalyzeDocumentDependencyChainAsync(IDocument document, CancellationToken cancellationToken);
    }
}
