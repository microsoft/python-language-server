﻿// Copyright(c) Microsoft Corporation
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
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Documents;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.LanguageServer.Sources {
    internal sealed class DocumentHighlightSource {
        private const int DocumentHighlightAnalysisTimeout = 1000;
        private static TokenCache _tokenCache = new TokenCache(10, TimeSpan.FromMinutes(30));
        private readonly IServiceContainer _services;

        public DocumentHighlightSource(IServiceContainer services) {
            _services = services;
        }

        public async Task<DocumentHighlight[]> DocumentHighlightAsync(Uri uri, SourceLocation location, CancellationToken cancellationToken = default) {
            if (uri == null) {
                return Array.Empty<DocumentHighlight>();
            }

            var analysis = await Document.GetAnalysisAsync(uri, _services, DocumentHighlightAnalysisTimeout, cancellationToken);
            var definitionSource = new DefinitionSource(_services);

            var definition = definitionSource.FindDefinition(analysis, location, out var definingMember);
            if (definition == null || definingMember == null) {
                return FromTokens(analysis, location);
            }

            var rootDefinition = definingMember.GetRootDefinition();

            var result = rootDefinition.References
                .Where(r => r.DocumentUri.Equals(uri))
                .Select((r, i) => new DocumentHighlight {
                    kind = i == 0 ? DocumentHighlightKind.Write : DocumentHighlightKind.Read, range = r.Span
                })
                .ToArray();

            return result;
        }

        private static DocumentHighlight[] FromTokens(IDocumentAnalysis analysis, SourceLocation location) {
            var position = analysis.Ast.LocationToIndex(location);
            var content = analysis.Document.Content;

            var tokens = _tokenCache.GetTokens(analysis.Document.Content, analysis.Document.Interpreter.LanguageVersion);
            var t = tokens.FirstOrDefault(x => x.SourceSpan.Start.Index <= position && position < x.SourceSpan.End.Index);
            if (t.Category != TokenCategory.None) {
                var length = t.SourceSpan.End.Index - t.SourceSpan.Start.Index;
                return tokens
                    .Where(x =>
                        x.SourceSpan.End.Index - x.SourceSpan.Start.Index == length &&
                        string.Compare(content, x.SourceSpan.Start.Index, content, t.SourceSpan.Start.Index, length) == 0)
                    .Select(s => new DocumentHighlight {
                        kind = DocumentHighlightKind.Text,
                        range = s.SourceSpan
                    }).ToArray();
            }

            return Array.Empty<DocumentHighlight>();
        }
    }


    internal class TokenCache {
        internal class Entry {
            public DateTime AccessTime;
            public WeakReference<IReadOnlyList<TokenInfo>> Tokens;
        }

        private readonly Dictionary<long, Entry> _cache = new Dictionary<long, Entry>();
        private readonly int _maxEntries;
        private readonly TimeSpan _expiration;

        public TokenCache(int maxEntries, TimeSpan expiration) {
            _maxEntries = maxEntries;
            _expiration = expiration;
        }

        public IReadOnlyList<TokenInfo> GetTokens(string content, PythonLanguageVersion languageVersion) {
            IReadOnlyList<TokenInfo> tokens;
            
            var hash = GetHash(content);
            if (_cache.TryGetValue(hash, out var entry)) {
                if (entry.Tokens.TryGetTarget(out tokens)) {
                    entry.AccessTime = DateTime.Now;
                    return tokens;
                }
            }

            var tokenizer = new Tokenizer(languageVersion);
            using (var sr = new StringReader(content)) {
                tokenizer.Initialize(null, sr, SourceLocation.MinValue);
                tokens = tokenizer.ReadTokens(content.Length);
                _cache[hash] = new Entry {
                    AccessTime = DateTime.Now,
                    Tokens = new WeakReference<IReadOnlyList<TokenInfo>>(tokens)
                };
            }

            var byTime = _cache.OrderByDescending(kvp => (DateTime.Now - kvp.Value.AccessTime).TotalSeconds).ToArray();

            var expired = byTime.TakeWhile(kvp => DateTime.Now - kvp.Value.AccessTime > _expiration).ToArray();
            foreach (var e in expired) {
                _cache.Remove(e.Key);
            }

            if (_cache.Count > _maxEntries) {
                var (key, _) = byTime.FirstOrDefault();
                if (key != default) {
                    _cache.Remove(key);
                }
            }

            return tokens;
        }

        // For tests
        internal IEnumerable<(DateTime, IReadOnlyList<TokenInfo>)> Entries
            => _cache.Values.Select(kvp => {
                kvp.Tokens.TryGetTarget(out var t);
                return (kvp.AccessTime, t);
            });

        internal static long GetHash(string content) {
            using (var sha = SHA1.Create()) {
                return BitConverter.ToInt64(sha.ComputeHash(Encoding.UTF32.GetBytes(content)));
            }
        }
    }
}
