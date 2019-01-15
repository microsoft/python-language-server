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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Indexing;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.Python.LanguageServer.Implementation {
    public sealed partial class Server {
        private static int _symbolHierarchyDepthLimit = 10;
        private static int _symbolHierarchyMaxSymbols = 1000;

        public override Task<SymbolInformation[]> WorkspaceSymbols(WorkspaceSymbolParams @params, CancellationToken cancellationToken) {
            var symbols = SymbolIndex.WorkspaceSymbols(@params.query);

            var result = symbols.Take(_symbolHierarchyMaxSymbols).Select(s => {
                cancellationToken.ThrowIfCancellationRequested();
                return new SymbolInformation {
                    name = s.Name,
                    kind = (SymbolKind)s.Kind,
                    location = new Location {
                        range = s.Range,
                        uri = s.DocumentUri,
                    },
                    containerName = s.ContainerName,
                };
            }).ToArray();

            return Task.FromResult(result);
        }

        public override Task<SymbolInformation[]> DocumentSymbol(DocumentSymbolParams @params, CancellationToken cancellationToken) {
            var uri = @params.textDocument.uri;
            var symbols = SymbolIndex.HierarchicalDocumentSymbols(uri);

            var result = symbols.Flatten(uri, depthLimit: _symbolHierarchyDepthLimit).Take(_symbolHierarchyMaxSymbols).Select(s => {
                cancellationToken.ThrowIfCancellationRequested();
                return new SymbolInformation {
                    name = s.Name,
                    kind = (SymbolKind)s.Kind,
                    location = new Location {
                        range = s.Range,
                        uri = s.DocumentUri,
                    },
                    containerName = s.ContainerName,
                };
            }).ToArray();

            return Task.FromResult(result);
        }

        public override Task<DocumentSymbol[]> HierarchicalDocumentSymbol(DocumentSymbolParams @params, CancellationToken cancellationToken) {
            var uri = @params.textDocument.uri;
            var symbols = SymbolIndex.HierarchicalDocumentSymbols(uri);
            var result = ToDocumentSymbols(symbols, cancellationToken);
            return Task.FromResult(result);
        }

        private static async Task<List<IMemberResult>> GetModuleVariablesAsync(ProjectEntry entry, GetMemberOptions opts, string prefix, int timeout, CancellationToken token) {
            var analysis = entry != null ? await entry.GetAnalysisAsync(timeout, token) : null;
            return analysis == null ? new List<IMemberResult>() : GetModuleVariables(entry, opts, prefix, analysis).ToList();
        }

        private static IEnumerable<IMemberResult> GetModuleVariables(ProjectEntry entry, GetMemberOptions opts, string prefix, IModuleAnalysis analysis) {
            var breadthFirst = analysis.Scope.TraverseBreadthFirst(s => s.Children);
            var all = breadthFirst.SelectMany(c => analysis.GetAllAvailableMembersFromScope(c, opts));
            var result = all
                .Where(m => {
                    if (m.Values.Any(v => v.DeclaringModule == entry ||
                        v.Locations
                            .MaybeEnumerate()
                            .ExcludeDefault()
                            .Any(l => l.DocumentUri == entry.DocumentUri))) {
                        return string.IsNullOrEmpty(prefix) || m.Name.StartsWithOrdinal(prefix, ignoreCase: true);
                    }
                    return false;
                })
                .Take(_symbolHierarchyMaxSymbols);
            return result;
        }

        private DocumentSymbol[] ToDocumentSymbols(IEnumerable<HierarchicalSymbol> hSymbols, CancellationToken cancellationToken = default(CancellationToken)) {
            return hSymbols.MaybeEnumerate().Select(hSym => new DocumentSymbol {
                name = hSym.Name,
                detail = hSym.Detail,
                kind = (SymbolKind)hSym.Kind,
                deprecated = hSym.Deprecated ?? false,
                range = hSym.Range,
                selectionRange = hSym.SelectionRange,
                children = ToDocumentSymbols(hSym.Children),
                _functionKind = hSym._functionKind,
            }).Select(v => {
                cancellationToken.ThrowIfCancellationRequested();

                if (v.selectionRange.start < v.range.start || v.selectionRange.end > v.range.end) {
                    Debug.Fail($"selectionRange {v.selectionRange} not encompassed by range {v.range}");
                    v.selectionRange = v.range;
                }

                return v;
            }).ToArray();
        }

        private SymbolInformation ToSymbolInformation(IMemberResult m) {
            var res = new SymbolInformation {
                name = m.Name,
                kind = ToSymbolKind(m.MemberType),
                _kind = m.MemberType.ToString().ToLowerInvariant()
            };

            var loc = m.Locations.FirstOrDefault(l => !string.IsNullOrEmpty(l.FilePath));

            if (loc != null) {
                res.location = new Location {
                    uri = loc.DocumentUri,
                    range = new SourceSpan(
                        new SourceLocation(loc.StartLine, loc.StartColumn),
                        new SourceLocation(loc.EndLine ?? loc.StartLine, loc.EndColumn ?? loc.StartColumn)
                    )
                };
            }

            return res;
        }

        private DocumentSymbol[] ToDocumentSymbols(List<IMemberResult> members) {
            var topLevel = new List<IMemberResult>();
            var childMap = new Dictionary<IMemberResult, List<IMemberResult>>();

            foreach (var m in members) {
                var parent = members.FirstOrDefault(x => x.Scope?.Node == m.Scope?.OuterScope?.Node && x.Name == m.Scope?.Name);
                if (parent != null) {
                    if (!childMap.TryGetValue(parent, out var children)) {
                        childMap[parent] = children = new List<IMemberResult>();
                    }
                    children.Add(m);
                } else {
                    topLevel.Add(m);
                }
            }

            var symbols = topLevel
                    .GroupBy(mr => mr.Name)
                    .Select(g => g.First())
                    .Select(m => ToDocumentSymbol(m, childMap, 0))
                    .ToArray();

            return symbols;
        }

        private DocumentSymbol ToDocumentSymbol(IMemberResult m, Dictionary<IMemberResult, List<IMemberResult>> childMap, int currentDepth) {
            var res = new DocumentSymbol {
                name = m.Name,
                detail = m.Name,
                kind = ToSymbolKind(m.MemberType),
                deprecated = false,
                _functionKind = GetFunctionKind(m)
            };

            if (childMap.TryGetValue(m, out var children) && currentDepth < _symbolHierarchyDepthLimit) {
                res.children = children
                    .Select(x => ToDocumentSymbol(x, childMap, currentDepth + 1))
                    .ToArray();
            } else {
                res.children = Array.Empty<DocumentSymbol>();
            }

            var loc = m.Locations.FirstOrDefault(l => !string.IsNullOrEmpty(l.FilePath));

            if (loc != null) {
                res.range = new SourceSpan(
                        new SourceLocation(loc.StartLine, loc.StartColumn),
                        new SourceLocation(loc.EndLine ?? loc.StartLine, loc.EndColumn ?? loc.StartColumn)
                    );
                res.selectionRange = res.range;
            }
            return res;
        }

        private static string GetFunctionKind(IMemberResult m) {
            if (m.MemberType == PythonMemberType.Function) {
                var funcInfo = m.Values.OfType<IFunctionInfo>().FirstOrDefault();
                if (funcInfo != null) {
                    if (funcInfo.IsProperty) {
                        return "property";
                    }
                    if (funcInfo.IsStatic) {
                        return "staticmethod";
                    }
                    if (funcInfo.IsClassMethod) {
                        return "classmethod";
                    }
                }
                return "function";
            }
            return m.MemberType == PythonMemberType.Class ? "class" : string.Empty;
        }

        private static SymbolKind ToSymbolKind(PythonMemberType memberType) {
            switch (memberType) {
                case PythonMemberType.Unknown: return SymbolKind.None;
                case PythonMemberType.Class: return SymbolKind.Class;
                case PythonMemberType.Instance: return SymbolKind.Variable;
                case PythonMemberType.Enum: return SymbolKind.Enum;
                case PythonMemberType.EnumInstance: return SymbolKind.EnumMember;
                case PythonMemberType.Function: return SymbolKind.Function;
                case PythonMemberType.Method: return SymbolKind.Method;
                case PythonMemberType.Module: return SymbolKind.Module;
                case PythonMemberType.Constant: return SymbolKind.Constant;
                case PythonMemberType.Event: return SymbolKind.Event;
                case PythonMemberType.Field: return SymbolKind.Field;
                case PythonMemberType.Property: return SymbolKind.Property;
                case PythonMemberType.Multiple: return SymbolKind.Object;
                case PythonMemberType.Keyword: return SymbolKind.None;
                case PythonMemberType.CodeSnippet: return SymbolKind.None;
                case PythonMemberType.NamedArgument: return SymbolKind.None;
                default: return SymbolKind.None;
            }
        }
    }
}
