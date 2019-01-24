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

using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.LanguageServer.Protocol;

namespace Microsoft.Python.LanguageServer.Implementation {
    public sealed partial class Server {
        public async Task<CompletionList> Completion(CompletionParams @params, CancellationToken cancellationToken) {
            var uri = @params.textDocument.uri;
            _log?.Log(TraceEventType.Verbose, $"Completions in {uri} at {@params.position}");

            var res = new CompletionList();
            var analysis = GetAnalysis(uri, @params.position, cancellationToken);
            if(analysis != null) { 
                var result = await _completionSource.GetCompletionsAsync(analysis, @params.position, cancellationToken);
                res.items = result.Completions.ToArray();
            }

            //await InvokeExtensionsAsync((ext, token)
            //    => (ext as ICompletionExtension)?.HandleCompletionAsync(uri, analysis, tree, @params.position, res, cancellationToken), cancellationToken);
            return res;
        }

        public Task<CompletionItem> CompletionItemResolve(CompletionItem item, CancellationToken token) {
            // TODO: Fill out missing values in item
            return Task.FromResult(item);
        }
    }
}
