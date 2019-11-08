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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Collections;
using Microsoft.Python.LanguageServer.CodeActions;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Sources {
    internal sealed partial class QuickFixCodeActionSource {
        private static readonly ImmutableArray<IQuickFixCodeActionProvider> _codeActionProviders =
            ImmutableArray<IQuickFixCodeActionProvider>.Create(MissingImportCodeActionProvider.Instance);

        private readonly IServiceContainer _services;

        public QuickFixCodeActionSource(IServiceContainer services) {
            _services = services;
        }

        public async Task<CodeAction[]> GetCodeActionsAsync(IDocumentAnalysis analysis, CodeActionSettings settings, Diagnostic[] diagnostics, CancellationToken cancellationToken) {
            var results = new List<CodeAction>();

            foreach (var diagnostic in GetMatchingDiagnostics(analysis, diagnostics, cancellationToken)) {
                foreach (var codeActionProvider in _codeActionProviders) {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (codeActionProvider.FixableDiagnostics.Any(code => code == diagnostic.ErrorCode)) {
                        results.AddRange(await codeActionProvider.GetCodeActionsAsync(analysis, settings, diagnostic, cancellationToken));
                    }
                }
            }

            return results.ToArray();
        }

        private IEnumerable<DiagnosticsEntry> GetMatchingDiagnostics(IDocumentAnalysis analysis, Diagnostic[] diagnostics, CancellationToken cancellationToken) {
            var diagnosticService = _services.GetService<IDiagnosticsService>();

            // we assume diagnostic service has the latest results
            if (diagnosticService == null || !diagnosticService.Diagnostics.TryGetValue(analysis.Document.Uri, out var latestDiagnostics)) {
                yield break;
            }

            foreach (var diagnostic in latestDiagnostics) {
                cancellationToken.ThrowIfCancellationRequested();

                if (diagnostics.Any(d => AreEqual(d, diagnostic))) {
                    yield return diagnostic;
                }
            }

            bool AreEqual(Diagnostic diagnostic1, DiagnosticsEntry diagnostic2) {
                return diagnostic1.code == diagnostic2.ErrorCode &&
                       diagnostic1.range.ToSourceSpan() == diagnostic2.SourceSpan;
            }
        }
    }
}
