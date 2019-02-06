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
using System.IO;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.LanguageServer.Completion {
    internal sealed class TokenSource {
        public IEnumerable<KeyValuePair<IndexSpan, Token>> Tokens { get; }
        public NewLineLocation[] TokenNewlines { get; }

        public SourceSpan GetTokenSpan(IndexSpan span) {
            return new SourceSpan(
                NewLineLocation.IndexToLocation(TokenNewlines, span.Start),
                NewLineLocation.IndexToLocation(TokenNewlines, span.End)
            );
        }

        public TokenSource(IDocument document, int toIndex) {
            var tokens = new List<KeyValuePair<IndexSpan, Token>>();
            Tokenizer tokenizer;
            using (var reader = new StringReader(document.Content)) {
                tokenizer = new Tokenizer(document.Interpreter.LanguageVersion, options: TokenizerOptions.GroupingRecovery);
                tokenizer.Initialize(reader);
                for (var t = tokenizer.GetNextToken();
                    t.Kind != TokenKind.EndOfFile && tokenizer.TokenSpan.Start < toIndex;
                    t = tokenizer.GetNextToken()) {
                    tokens.Add(new KeyValuePair<IndexSpan, Token>(tokenizer.TokenSpan, t));
                }
            }

            Tokens = tokens;
            TokenNewlines = tokenizer.GetLineLocations();
        }

    }
}
