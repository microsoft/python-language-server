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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Logging;

namespace Microsoft.Python.LanguageServer.Documents {
    internal static class Document {
        public static async Task<IDocumentAnalysis> GetAnalysisAsync(Uri uri, IServiceContainer services, int msTimeout = 300, CancellationToken cancellationToken = default) {
            var rdt = services.GetService<IRunningDocumentTable>();
            var document = rdt.GetDocument(uri);
            if (document == null) {
                var log = services.GetService<ILogger>();
                log?.Log(TraceEventType.Error, $"Unable to find document {uri}");
                return null;
            }
            return await document.GetAnalysisAsync(msTimeout, cancellationToken);
        }
    }
}
