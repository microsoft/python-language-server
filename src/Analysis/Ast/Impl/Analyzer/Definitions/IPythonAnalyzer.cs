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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    public interface IPythonAnalyzer {
        Task WaitForCompleteAnalysisAsync(CancellationToken cancellationToken = default);
        /// <summary>
        /// Schedules module for analysis. Module will be scheduled if version of AST is greater than the one used to get previous analysis
        /// </summary>
        void EnqueueDocumentForAnalysis(IPythonModule module, int version);

        /// <summary>
        /// Schedules module for analysis for its existing AST, but with new dependencies.
        /// Module will be scheduled if any of the dependencies has analysis version greater than the module.
        /// </summary>
        void EnqueueDocumentForAnalysis(IPythonModule module, ImmutableArray<IPythonModule> dependencies);
        
        /// <summary>
        /// Invalidates current analysis for the module, assuming that AST for the new analysis will be provided later.
        /// </summary>
        void InvalidateAnalysis(IPythonModule module);

        /// <summary>
        /// Removes modules from the analysis.
        /// </summary>
        void RemoveAnalysis(IPythonModule module);

        /// <summary>
        /// Get most recent analysis for module. If after specified time analysis isn't available, returns previously calculated analysis.
        /// </summary>
        Task<IDocumentAnalysis> GetAnalysisAsync(IPythonModule module, int waitTime = 200, CancellationToken cancellationToken = default);

        /// <summary>
        /// Runs linters on the modules
        /// </summary>
        IReadOnlyList<DiagnosticsEntry> LintModule(IPythonModule module);

        /// <summary>
        /// Removes all the modules from the analysis, except Typeshed and builtin
        /// </summary>
        void ResetAnalyzer();

        /// <summary>
        /// Returns list of currently loaded modules.
        /// </summary>
        IReadOnlyList<IPythonModule> LoadedModules { get; }
    }
}
