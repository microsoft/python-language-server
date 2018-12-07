﻿// Python Tools for Visual Studio
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
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Implementation {
    public sealed partial class Server {
        public override async Task<Reference[]> FindReferences(ReferencesParams @params, CancellationToken cancellationToken) {
            await WaitForCompleteAnalysisAsync(cancellationToken);

            var uri = @params.textDocument.uri;
            ProjectFiles.GetEntry(@params.textDocument, @params._version, out var entry, out var tree);

            TraceMessage($"References in {uri} at {@params.position}");

            var analysis = entry != null ? await entry.GetAnalysisAsync(50, cancellationToken) : null;
            if (analysis == null) {
                TraceMessage($"No analysis found for {uri}");
                return Array.Empty<Reference>();
            }

            tree = GetParseTree(entry, uri, cancellationToken, out var version);
            var modRefs = GetModuleReferences(entry, tree, version, @params);

            IEnumerable<IAnalysisVariable> result;
            if (!string.IsNullOrEmpty(@params._expr)) {
                TraceMessage($"Getting references for {@params._expr}");
                result = analysis.GetVariables(@params._expr, @params.position);
            } else {
                var finder = new ExpressionFinder(tree, GetExpressionOptions.FindDefinition);
                if (finder.GetExpression(@params.position) is Expression expr) {
                    TraceMessage($"Getting references for {expr.ToCodeString(tree, CodeFormattingOptions.Traditional)}");
                    result = analysis.GetVariables(expr, @params.position);
                } else {
                    TraceMessage($"No references found in {uri} at {@params.position}");
                    result = Enumerable.Empty<IAnalysisVariable>();
                }
            }

            var filtered = result.Where(v => v.Type != VariableType.None);
            if (!(@params.context?.includeDeclaration ?? false)) {
                filtered = filtered.Where(v => v.Type != VariableType.Definition);
            }
            if (!(@params.context?._includeValues ?? false)) {
                filtered = filtered.Where(v => v.Type != VariableType.Value);
            }

            var res = filtered.Select(v => new Reference {
                uri = v.Location.DocumentUri,
                range = v.Location.Span,
                _kind = ToReferenceKind(v.Type),
                _version = version?.Version
            })
                .Concat(modRefs)
                .GroupBy(r => r, ReferenceComparer.Instance)
                .Select(g => g.OrderByDescending(r => (SourceLocation)r.range.end).ThenBy(r => (int?)r._kind ?? int.MaxValue).First())
                .ToArray();

            return res;
        }

        private IEnumerable<Reference> GetModuleReferences(IPythonProjectEntry entry, PythonAst tree, BufferVersion version, ReferencesParams @params) {
            if (!@params.context?.includeDeclaration == true) {
                return Enumerable.Empty<Reference>();
            }

            var index = tree.LocationToIndex(@params.position);
            var w = new ImportedModuleNameWalker(entry, index, tree);
            tree.Walk(w);

            if (w.ImportedType != null) {
                @params._expr = w.ImportedType.Name;
                return Enumerable.Empty<Reference>();
            }

            var modulesNamesInRange = w.ImportedModules.Where(m => {
                var start = tree.LocationToIndex(m.SourceSpan.Start);
                var end = tree.LocationToIndex(m.SourceSpan.End);
                return start <= index && index < end;
            }).ToArray();

            if (modulesNamesInRange.Length == 0) {
                return Enumerable.Empty<Reference>();
            }

            var refs = new List<Reference>();
            foreach (var n in modulesNamesInRange) {
                if (Analyzer.Modules.TryGetImportedModule(n.Name, out var modRef) && modRef.AnalysisModule != null) {
                    // Return a module reference
                    refs.AddRange(modRef.AnalysisModule.Locations
                        .Select(l => new Reference {
                            uri = l.DocumentUri,
                            range = l.Span,
                            _version = version?.Version,
                            _kind = ReferenceKind.Definition
                        })
                        .ToArray());
                }
            }
            return refs;
        }

        private static ReferenceKind ToReferenceKind(VariableType type) {
            switch (type) {
                case VariableType.None: return ReferenceKind.Value;
                case VariableType.Definition: return ReferenceKind.Definition;
                case VariableType.Reference: return ReferenceKind.Reference;
                case VariableType.Value: return ReferenceKind.Value;
                default: return ReferenceKind.Value;
            }
        }

        private sealed class ReferenceComparer : IEqualityComparer<Reference> {
            public static readonly IEqualityComparer<Reference> Instance = new ReferenceComparer();
            private ReferenceComparer() { }
            public bool Equals(Reference x, Reference y)
                => x.uri == y.uri && (SourceLocation)x.range.start == y.range.start;

            public int GetHashCode(Reference obj)
                => new { u = obj.uri, l = obj.range.start.line, c = obj.range.start.character }.GetHashCode();
        }
    }
}
