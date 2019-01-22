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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Completion {
    internal class ImportCompletion {
        public static async Task<CompletionResult> GetCompletionsInImportAsync(ImportStatement import, CompletionContext context, CancellationToken cancellationToken = default) {
            // No names, so if we are at the end. Return available modules
            if (import.Names.Count == 0 && context.Position > import.KeywordEndIndex) {
                return new CompletionResult(await GetModulesAsync(context, cancellationToken));
            }

            foreach (var (name, asName) in ZipLongest(import.Names, import.AsNames).Reverse()) {
                if (asName != null && context.Position >= asName.StartIndex) {
                    return CompletionResult.Empty;
                }

                if (name != null && context.Position >= name.StartIndex) {
                    if (context.Position > name.EndIndex && name.EndIndex > name.StartIndex) {
                        var applicableSpan = context.GetApplicableSpanFromLastToken(import);
                        return new CompletionResult(Enumerable.Repeat(CompletionItemSource.AsKeyword, 1), applicableSpan);
                    }
                    var nex = name.Names.LastOrDefault(n => n.StartIndex <= context.Position && context.Position <= n.EndIndex);
                    if (nex != null) {
                        var mres = context.Analysis.Document.Interpreter.ModuleResolution;
                        var imp = mres.CurrentPathResolver.GetModuleImportFromModuleName(nex.Name);
                        if (imp != null) {
                            var mod = mres.GetImportedModule(imp.FullName);
                            if (mod != null) {
                                var items = mod.GetMemberNames()
                                    .Select(n => context.ItemSource.CreateCompletionItem(n, mod.GetMember(n)));
                                return new CompletionResult(items);
                            }
                        }
                    }
                }
            }
            return CompletionResult.Empty;
        }

        public static async Task<CompletionResult> GetCompletionsInFromImportAsync(FromImportStatement fromImport, CompletionContext context, CancellationToken cancellationToken = default) {
            // No more completions after '*', ever!
            if (fromImport.Names != null && fromImport.Names.Any(n => n?.Name == "*" && context.Position > n.EndIndex)) {
                return CompletionResult.Empty;
            }

            var document = context.Analysis.Document;
            var mres = document.Interpreter.ModuleResolution;

            foreach (var (name, asName) in ZipLongest(fromImport.Names, fromImport.AsNames).Reverse()) {
                if (asName != null && context.Position >= asName.StartIndex) {
                    return CompletionResult.Empty;
                }

                if (name != null) {
                    if (context.Position > name.EndIndex && name.EndIndex > name.StartIndex) {
                        var applicableSpan = context.GetApplicableSpanFromLastToken(fromImport);
                        return new CompletionResult(Enumerable.Repeat(CompletionItemSource.AsKeyword, 1), applicableSpan);
                    }

                    if (context.Position >= name.StartIndex) {
                        var applicableSpan = name.GetSpan(context.Ast);
                        var importSearchResult = mres.CurrentPathResolver.FindImports(name.Name, fromImport);
                        var items = GetResultFromSearch(importSearchResult, context).Completions;
                        return new CompletionResult(items, applicableSpan);
                    }
                }
            }

            if (fromImport.ImportIndex > fromImport.StartIndex && context.Position > fromImport.ImportIndex + 6) {
                var importSearchResult = mres.CurrentPathResolver.FindImports(document.FilePath, fromImport);
                var result = GetResultFromSearch(importSearchResult, context);
                if (result != CompletionResult.Empty) {
                    return result;
                }
            }

            if (context.Position >= fromImport.ImportIndex) {
                var applicableSpan = new SourceSpan(
                    context.IndexToLocation(fromImport.ImportIndex),
                    context.IndexToLocation(Math.Min(fromImport.ImportIndex + 6, fromImport.EndIndex))
                );
                return new CompletionResult(Enumerable.Repeat(CompletionItemSource.ImportKeyword, 1), applicableSpan);
            }

            if (context.Position > fromImport.Root.EndIndex && fromImport.Root.EndIndex > fromImport.Root.StartIndex) {
                SourceSpan? applicableSpan = null;
                if (context.Position > fromImport.EndIndex) {
                    // Only end up here for "from ... imp", and "imp" is not counted
                    // as part of our span
                    var token = context.TokenSource.Tokens.LastOrDefault();
                    if (token.Key.End >= context.Position) {
                        applicableSpan = context.TokenSource.GetTokenSpan(token.Key);
                    }
                }

                return new CompletionResult(Enumerable.Repeat(CompletionItemSource.ImportKeyword, 1), applicableSpan);
            }

            if (context.Position >= fromImport.Root.StartIndex) {
                var importSearchResult = mres.CurrentPathResolver.FindImports(document.FilePath, fromImport);
                return GetResultFromSearch(importSearchResult, context);
            }

            return context.Position > fromImport.KeywordEndIndex
                ? new CompletionResult(await GetModulesAsync(context, cancellationToken))
                : CompletionResult.Empty;
        }

        private static async Task<IEnumerable<CompletionItem>> GetModulesAsync(CompletionContext context, CancellationToken cancellationToken = default) {
            var mres = context.Analysis.Document.Interpreter.ModuleResolution;
            var modules = await mres.GetImportableModulesAsync(cancellationToken);
            return modules.Select(kvp => CompletionItemSource.CreateCompletionItem(kvp.Key, CompletionItemKind.Module));
        }

        private static CompletionResult GetResultFromSearch(IImportSearchResult importSearchResult, CompletionContext context) {
            var document = context.Analysis.Document;
            var mres = document.Interpreter.ModuleResolution;

            IPythonModule module;
            switch (importSearchResult) {
                case ModuleImport moduleImports:
                    module = mres.GetImportedModule(moduleImports.Name);
                    break;
                case PossibleModuleImport possibleModuleImport:
                    module = mres.GetImportedModule(possibleModuleImport.PossibleModuleFullName);
                    break;
                case PackageImport packageImports:
                    return new CompletionResult(packageImports.Modules
                        .Select(m => mres.GetImportedModule(m.FullName))
                        .ExcludeDefault()
                        .Select(m => CompletionItemSource.CreateCompletionItem(m.Name, CompletionItemKind.Module))
                        .Prepend(CompletionItemSource.Star));
                default:
                    return CompletionResult.Empty;
            }

            if (module != null) {
                var items = module.GetMemberNames()
                    .Select(n => context.ItemSource.CreateCompletionItem(n, module.GetMember(n)))
                    .Prepend(CompletionItemSource.Star);
                return new CompletionResult(items);
            }
            return CompletionResult.Empty;
        }

        private static IEnumerable<(T1, T2)> ZipLongest<T1, T2>(IEnumerable<T1> src1, IEnumerable<T2> src2) {
            using (var e1 = src1?.GetEnumerator())
            using (var e2 = src2?.GetEnumerator()) {
                bool b1 = e1?.MoveNext() ?? false, b2 = e2?.MoveNext() ?? false;
                while (b1 && b2) {
                    yield return (e1.Current, e2.Current);
                    b1 = e1.MoveNext();
                    b2 = e2.MoveNext();
                }

                while (b1) {
                    yield return (e1.Current, default);
                    b1 = e1.MoveNext();
                }

                while (b2) {
                    yield return (default, e2.Current);
                    b2 = e2.MoveNext();
                }
            }
        }
    }
}
