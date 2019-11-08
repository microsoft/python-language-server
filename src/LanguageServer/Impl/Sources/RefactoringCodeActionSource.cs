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
using Microsoft.Python.Analysis;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.CodeActions;
using Microsoft.Python.LanguageServer.Protocol;

namespace Microsoft.Python.LanguageServer.Sources {
    internal sealed partial class RefactoringCodeActionSource {
        private static readonly ImmutableArray<IRefactoringCodeActionProvider> _codeActionProviders =
            ImmutableArray<IRefactoringCodeActionProvider>.Empty;

        private readonly IServiceContainer _services;

        public RefactoringCodeActionSource(IServiceContainer services) {
            _services = services;
        }

        public async Task<CodeAction[]> GetCodeActionsAsync(IDocumentAnalysis analysis, CodeActionSettings settings, Range range, CancellationToken cancellationToken) {
            var results = new List<CodeAction>();
            foreach (var codeActionProvider in _codeActionProviders) {
                cancellationToken.ThrowIfCancellationRequested();

                results.AddRange(await codeActionProvider.GetCodeActionsAsync(analysis, settings, range, cancellationToken));
            }

            return results.ToArray();
        }
    }
}
