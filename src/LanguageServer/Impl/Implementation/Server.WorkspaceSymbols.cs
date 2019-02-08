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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Core;
using Microsoft.Python.LanguageServer.Indexing;
using Microsoft.Python.LanguageServer.Protocol;

namespace Microsoft.Python.LanguageServer.Implementation {
    public sealed partial class Server {
        private static int _symbolHierarchyDepthLimit = 10;
        private static int _symbolHierarchyMaxSymbols = 1000;

        public async Task<SymbolInformation[]> WorkspaceSymbols(WorkspaceSymbolParams @params, CancellationToken cancellationToken) {
            var symbols = await _indexManager.WorkspaceSymbolsAsync(@params.query,
                                                                    _symbolHierarchyMaxSymbols,
                                                                    cancellationToken);
            return symbols.Select(MakeSymbolInfo).ToArray();
        }

        public async Task<DocumentSymbol[]> HierarchicalDocumentSymbol(DocumentSymbolParams @params, CancellationToken cancellationToken) {
            var path = @params.textDocument.uri.AbsolutePath;
            var symbols = await _indexManager.HierarchicalDocumentSymbolsAsync(path, cancellationToken);
            return symbols.MaybeEnumerate().Select(hSym => MakeDocumentSymbol(hSym)).ToArray();
        }

        private static SymbolInformation MakeSymbolInfo(Indexing.FlatSymbol s) {
            return new SymbolInformation {
                name = s.Name,
                kind = (Protocol.SymbolKind)s.Kind,
                location = new Location {
                    range = s.Range,
                    uri = new Uri(s.DocumentPath),
                },
                containerName = s.ContainerName,
            };
        }

        private DocumentSymbol MakeDocumentSymbol(HierarchicalSymbol hSym) {
            return new DocumentSymbol {
                name = hSym.Name,
                detail = hSym.Detail,
                kind = (Protocol.SymbolKind)hSym.Kind,
                deprecated = hSym.Deprecated ?? false,
                range = hSym.Range,
                selectionRange = hSym.SelectionRange,
                children = hSym.Children.MaybeEnumerate().Select(MakeDocumentSymbol).ToArray(),
            };
        }
    }
}
