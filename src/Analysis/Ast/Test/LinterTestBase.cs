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
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Parsing.Tests;

namespace Microsoft.Python.Analysis.Tests {
    public abstract class LinterTestBase: AnalysisTestBase {
        protected async Task<IReadOnlyList<DiagnosticsEntry>> LintAsync(string code, InterpreterConfiguration configuration = null, bool runIsolated = false) {
            configuration = configuration ?? PythonVersions.LatestAvailable3X;
            var analysis = await GetAnalysisAsync(code, configuration, runIsolated);
            var services = runIsolated ? Services : GetSharedServices(configuration);
            var a = services.GetService<IPythonAnalyzer>();
            return a.LintModule(analysis.Document);
        }

        protected class AnalysisOptionsProvider : IAnalysisOptionsProvider {
            public AnalysisOptions Options { get; } = new AnalysisOptions();
        }

    }
}
