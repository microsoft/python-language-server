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
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.CodeActions;
using Microsoft.Python.LanguageServer.Protocol;

namespace Microsoft.Python.LanguageServer {
    public interface IRefactoringCodeActionProvider {
        /// <summary>
        /// Returns <see cref="CodeAction" /> for the given <paramref name="range"/>. What it would do is up to the refactoring
        /// </summary>
        /// <param name="analysis"><see cref="IDocumentAnalysis" /> of the file where <paramref name="range"/> exists</param>
        /// <param name="settings">settings related to code actions one can query to get user preferences</param>
        /// <param name="range">range where refactoring is called upon</param>
        /// <param name="cancellation"><see cref="CancellationToken" /></param>
        /// <returns><see cref="CodeAction" /> that will update user code or context</returns>
        Task<IEnumerable<CodeAction>> GetCodeActionsAsync(IDocumentAnalysis analysis, CodeActionSettings settings, Range range, CancellationToken cancellation);
    }
}
