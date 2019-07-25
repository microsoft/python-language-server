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
using System.Linq;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Linting.UndefinedVariables;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Linting {
    internal sealed class LinterAggregator {
        private readonly List<ILinter> _linters = new List<ILinter>();

        public LinterAggregator() {
            // TODO: develop mechanism for dynamic and external linter discovery.
            _linters.Add(new UndefinedVariablesLinter());
        }
        public IReadOnlyList<DiagnosticsEntry> Lint(IPythonModule module, IServiceContainer services)
            => _linters.SelectMany(l => l.Lint(module.Analysis, services)).Where(d => d.ShouldReport(module)).ToArray();
    }
}
