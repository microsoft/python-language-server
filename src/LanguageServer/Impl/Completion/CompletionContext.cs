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

using System.Linq;
using Microsoft.Python.Analysis;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Completion {
    internal sealed class CompletionContext {
        private TokenSource _ts;

        public IDocumentAnalysis Analysis { get; }
        public PythonAst Ast => Analysis.Ast;
        public SourceLocation Location { get; }
        public int Position { get; }
        public TokenSource TokenSource => _ts ?? (_ts = new TokenSource(Analysis.Document, Position));
        public CompletionItemSource ItemSource { get; }
        public IServiceContainer Services { get; }

        public CompletionContext(IDocumentAnalysis analysis, SourceLocation location, CompletionItemSource itemSource, IServiceContainer services) {
            Location = location;
            Analysis = analysis;
            Position = Ast.LocationToIndex(location);
            ItemSource = itemSource;
            Services = services;
        }

        public SourceLocation IndexToLocation(int index) => Ast.IndexToLocation(index);

        public SourceSpan GetApplicableSpanFromLastToken(Node containingNode) {
            if (containingNode != null && Position >= containingNode.EndIndex) {
                var token = TokenSource.Tokens.LastOrDefault();
                if (token.Key.End >= Position) {
                    return TokenSource.GetTokenSpan(token.Key);
                }
            }
            return default;
        }
    }
}
