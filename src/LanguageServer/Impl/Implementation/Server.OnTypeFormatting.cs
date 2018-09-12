// Python Tools for Visual Studio
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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;

namespace Microsoft.Python.LanguageServer.Implementation {
    public sealed partial class Server {
        public override async Task<TextEdit[]> DocumentOnTypeFormatting(DocumentOnTypeFormattingParams @params, CancellationToken cancellationToken) {
            // The current line is the line after the one we need to format, so
            // it is also the one-indexed line number for the target line.
            var targetLine = @params.position.line;
            if (@params.ch == ";") {
                // Unless the trigger was a semicolon, in which case the current line is the target.
                targetLine++;
            }

            var uri = @params.textDocument.uri;

            if (!(ProjectFiles.GetEntry(uri) is IDocument doc)) {
                return Array.Empty<TextEdit>();
            }
            var part = ProjectFiles.GetPart(uri);

            using (var reader = doc.ReadDocument(part, out _)) {
                var lineFormatter = new LineFormatter(reader, Analyzer.LanguageVersion);
                return lineFormatter.FormatLine(targetLine);
            }
        }
    }
}
