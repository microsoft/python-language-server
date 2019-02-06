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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Core;
using Microsoft.Python.LanguageServer.Formatting;
using Microsoft.Python.LanguageServer.Protocol;

namespace Microsoft.Python.LanguageServer.Implementation {
    public sealed partial class Server {
        public async Task<TextEdit[]> DocumentOnTypeFormatting(DocumentOnTypeFormattingParams @params, CancellationToken cancellationToken) {
            int targetLine;

            switch (@params.ch) {
                case "\n":
                    targetLine = @params.position.line - 1;
                    break;
                case ";":
                case ":":
                    targetLine = @params.position.line;
                    break;
                default:
                    throw new ArgumentException("unexpected trigger character", nameof(@params.ch));
            }

            var uri = @params.textDocument.uri;
            var doc = _rdt.GetDocument(uri);
            if (doc == null) {
                return Array.Empty<TextEdit>();
            }

            using (var reader = new StringReader(doc.Content)) {
                if (@params.ch == ":") {
                    return await BlockFormatter.ProvideEdits(reader, @params.position, @params.options);
                }

                var lineFormatter = new LineFormatter(reader, doc.Interpreter.LanguageVersion);
                var edits = lineFormatter.FormatLine(targetLine);
                var unmatchedToken = lineFormatter.UnmatchedToken(targetLine);

                if (unmatchedToken != null) {
                    var message = Resources.LineFormatter_UnmatchedToken.FormatInvariant(unmatchedToken.Value.token, unmatchedToken.Value.line + 1);
                    _log?.Log(TraceEventType.Warning, message);
                }

                return edits;
            }
        }
    }
}
