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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.Python.LanguageServer.Implementation {
    public sealed partial class Server {
        private static int _symbolHierarchyDepthLimit = 10;
        private static int _symbolHierarchyMaxSymbols = 1000;

        public override async Task<SymbolInformation[]> WorkspaceSymbols(WorkspaceSymbolParams @params, CancellationToken cancellationToken) {
            await WaitForCompleteAnalysisAsync(cancellationToken);

            var members = Enumerable.Empty<IMemberResult>();
            var opts = GetMemberOptions.ExcludeBuiltins | GetMemberOptions.DeclaredOnly;

            foreach (var entry in ProjectFiles) {
                members = members.Concat(
                    await GetModuleVariablesAsync(entry as ProjectEntry, opts, @params.query, 50, cancellationToken)
                );
            }

            members = members.GroupBy(mr => mr.Name).Select(g => g.First());
            return members.Select(ToSymbolInformation).ToArray();
        }

        public override async Task<SymbolInformation[]> DocumentSymbol(DocumentSymbolParams @params, CancellationToken cancellationToken) {
            await WaitForCompleteAnalysisAsync(cancellationToken);
            var opts = GetMemberOptions.ExcludeBuiltins | GetMemberOptions.DeclaredOnly;
            var entry = ProjectFiles.GetEntry(@params.textDocument.uri);

            var members = await GetModuleVariablesAsync(entry as ProjectEntry, opts, string.Empty, 50, cancellationToken);
            return members
                .GroupBy(mr => mr.Name)
                .Select(g => g.First())
                .Select(ToSymbolInformation)
                .ToArray();
        }

        public override async Task<DocumentSymbol[]> HierarchicalDocumentSymbol(DocumentSymbolParams @params, CancellationToken cancellationToken) {
            await WaitForCompleteAnalysisAsync(cancellationToken);
            var opts = GetMemberOptions.ExcludeBuiltins | GetMemberOptions.DeclaredOnly;
            var entry = ProjectFiles.GetEntry(@params.textDocument.uri);

            var members = await GetModuleVariablesAsync(entry as ProjectEntry, opts, string.Empty, 50, cancellationToken);
            return ToDocumentSymbols(members);
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
                case PythonMemberType.Delegate: return SymbolKind.Function;
                case PythonMemberType.DelegateInstance: return SymbolKind.Function;
                case PythonMemberType.Enum: return SymbolKind.Enum;
                case PythonMemberType.EnumInstance: return SymbolKind.EnumMember;
                case PythonMemberType.Function: return SymbolKind.Function;
                case PythonMemberType.Method: return SymbolKind.Method;
                case PythonMemberType.Module: return SymbolKind.Module;
                case PythonMemberType.Namespace: return SymbolKind.Namespace;
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
