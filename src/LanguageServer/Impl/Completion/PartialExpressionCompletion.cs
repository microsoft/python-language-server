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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;
using Nerdbank.Streams;

namespace Microsoft.Python.LanguageServer.Completion {
    internal sealed class PartialExpressionCompletion {
        private readonly Node _node;
        private readonly TokenSource _ts;

        public PartialExpressionCompletion(Node node, TokenSource ts) {
            _node = node;
            _ts = ts;
        }

        public SourceSpan ApplicableSpan { get; private set; }

        public IEnumerable<CompletionItem> GetCompletions(SourceLocation position) {
            if (!(_node is ErrorExpression)) {
                return null;
            }

            if (Statement is AssignmentStatement assign && _node == assign.Right) {
                return null;
            }

            bool ScopeIsClassDefinition(out ClassDefinition classDefinition) {
                classDefinition = Scope as ClassDefinition ?? (Scope as FunctionDefinition)?.Parent as ClassDefinition;
                return classDefinition != null;
            }

            var tokens = _ts.Tokens.Reverse().ToArray();
            var es = new ExpressionSource(_ts);

            string exprString;
            SourceLocation loc;
            var lastToken = tokens.FirstOrDefault();
            var nextLast = tokens.ElementAtOrDefault(1).Value?.Kind ?? TokenKind.EndOfFile;
            switch (lastToken.Value.Kind) {
                case TokenKind.Dot:
                    exprString = es.ReadExpression(tokens.Skip(1));
                    ApplicableSpan = new SourceSpan(position, position);
                    return Analysis.GetMembers(exprString, position, MultiplexingStream.Options).Select(ToCompletionItem);

                case TokenKind.KeywordDef when lastToken.Key.End < Index && ScopeIsClassDefinition(out var cd):
                    ApplicableSpan = new SourceSpan(position, position);
                    loc = _ts.GetTokenSpan(lastToken.Key).Start;
                    ShouldCommitByDefault = false;
                    return Analysis.GetOverrideable(loc).Select(o => ToOverrideCompletionItem(o, cd, new string(' ', loc.Column - 1)));

                case TokenKind.Name when nextLast == TokenKind.Dot:
                    exprString = es.ReadExpression(tokens.Skip(2));
                    ApplicableSpan = new SourceSpan(_ts.GetTokenSpan(lastToken.Key).Start, Position);
                    return Analysis.GetMembers(exprString, position, MultiplexingStream.Options).Select(ToCompletionItem);

                case TokenKind.Name when nextLast == TokenKind.KeywordDef && ScopeIsClassDefinition(out var cd):
                    ApplicableSpan = new SourceSpan(_ts.GetTokenSpan(lastToken.Key).Start, position);
                    loc = _ts.GetTokenSpan(tokens.ElementAt(1).Key).Start;
                    ShouldCommitByDefault = false;
                    return Analysis.GetOverrideable(loc).Select(o => ToOverrideCompletionItem(o, cd, new string(' ', loc.Column - 1)));

                case TokenKind.KeywordFor:
                case TokenKind.KeywordAs:
                    return lastToken.Key.Start <= Index && Index <= lastToken.Key.End ? null : Empty;

                default:
                    Debug.WriteLine($"Unhandled completions from error.\nTokens were: ({lastToken.Value.Image}:{lastToken.Value.Kind}), {string.Join(", ", tokens.AsEnumerable().Take(10).Select(t => $"({t.Value.Image}:{t.Value.Kind})"))}");
                    return null;
            }
        }
    }
}
