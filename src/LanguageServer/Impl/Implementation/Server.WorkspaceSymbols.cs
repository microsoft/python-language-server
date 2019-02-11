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
                kind = ToSymbolKind(hSym.Kind),
                deprecated = hSym.Deprecated ?? false,
                range = hSym.Range,
                selectionRange = hSym.SelectionRange,
                children = hSym.Children.MaybeEnumerate().Select(MakeDocumentSymbol).ToArray(),
            };
        }

        private Protocol.SymbolKind ToSymbolKind(Indexing.SymbolKind kind) {
            switch (kind) {
                case Indexing.SymbolKind.None:
                    return Protocol.SymbolKind.None;
                case Indexing.SymbolKind.File:
                    return Protocol.SymbolKind.File;
                case Indexing.SymbolKind.Module:
                    return Protocol.SymbolKind.Module;
                case Indexing.SymbolKind.Namespace:
                    return Protocol.SymbolKind.Namespace;
                case Indexing.SymbolKind.Package:
                    return Protocol.SymbolKind.Package;
                case Indexing.SymbolKind.Class:
                    return Protocol.SymbolKind.Class;
                case Indexing.SymbolKind.Method:
                    return Protocol.SymbolKind.Method;
                case Indexing.SymbolKind.Property:
                    return Protocol.SymbolKind.Property;
                case Indexing.SymbolKind.Field:
                    return Protocol.SymbolKind.Field;
                case Indexing.SymbolKind.Constructor:
                    return Protocol.SymbolKind.Constructor;
                case Indexing.SymbolKind.Enum:
                    return Protocol.SymbolKind.Enum;
                case Indexing.SymbolKind.Interface:
                    return Protocol.SymbolKind.Interface;
                case Indexing.SymbolKind.Function:
                    return Protocol.SymbolKind.Function;
                case Indexing.SymbolKind.Variable:
                    return Protocol.SymbolKind.Variable;
                case Indexing.SymbolKind.Constant:
                    return Protocol.SymbolKind.Constant;
                case Indexing.SymbolKind.String:
                    return Protocol.SymbolKind.String;
                case Indexing.SymbolKind.Number:
                    return Protocol.SymbolKind.Number;
                case Indexing.SymbolKind.Boolean:
                    return Protocol.SymbolKind.Boolean;
                case Indexing.SymbolKind.Array:
                    return Protocol.SymbolKind.Array;
                case Indexing.SymbolKind.Object:
                    return Protocol.SymbolKind.Object;
                case Indexing.SymbolKind.Key:
                    return Protocol.SymbolKind.Key;
                case Indexing.SymbolKind.Null:
                    return Protocol.SymbolKind.Null;
                case Indexing.SymbolKind.EnumMember:
                    return Protocol.SymbolKind.EnumMember;
                case Indexing.SymbolKind.Struct:
                    return Protocol.SymbolKind.Struct;
                case Indexing.SymbolKind.Event:
                    return Protocol.SymbolKind.Event;
                case Indexing.SymbolKind.Operator:
                    return Protocol.SymbolKind.Operator;
                case Indexing.SymbolKind.TypeParameter:
                    return Protocol.SymbolKind.TypeParameter;
                default:
                    throw new NotImplementedException($"{kind} is not a LSP's SymbolKind");
            }
        }
    }
}
