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
using Microsoft.Python.Core;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Completion {
    internal class ImportCompletion {
        public static async Task<CompletionResult> GetCompletionsInImportAsync(ImportStatement import, CompletionContext context, CancellationToken cancellationToken = default) {
            // No names, so if we are at the end. Return available modules
            if (import.Names.Count == 0 && context.Position > import.KeywordEndIndex) {
                return new CompletionResult(GetModules(context));
            }

            foreach (var (name, asName) in ZipLongest(import.Names, import.AsNames).Reverse()) {
                if (asName != null && context.Position >= asName.StartIndex) {
                    return true;
                }

                if (name != null && context.Position >= name.StartIndex) {
                    if (context.Position > name.EndIndex && name.EndIndex > name.StartIndex) {
                        var applicableSpan = context.GetApplicableSpanFromLastToken(import);
                        return new CompletionResult(Enumerable.Repeat(CompletionItemSource.AsKeyword, 1), applicableSpan);
                    } else {
                        Node = name.Names.LastOrDefault(n => n.StartIndex <= context.Position && context.Position <= n.EndIndex);
                        return await GetModulesFromNodeAsync(name, cancellationToken);
                    }
                }
            }
            return null;
        }

        public static async Task<CompletionResult> GetCompletionsInFromImportAsync(FromImportStatement fromImport, CompletionContext context, CancellationToken cancellationToken = default) {
            // No more completions after '*', ever!
            if (fromImport.Names != null && fromImport.Names.Any(n => n?.Name == "*" && context.Position > n.EndIndex)) {
                return true;
            }

            foreach (var (name, asName) in ZipLongest(fromImport.Names, fromImport.AsNames).Reverse()) {
                if (asName != null && context.Position >= asName.StartIndex) {
                    return true;
                }

                if (name != null) {
                    if (context.Position > name.EndIndex && name.EndIndex > name.StartIndex) {
                        var applicableSpan = context.GetApplicableSpanFromLastToken(fromImport);
                        return new CompletionResult(Enumerable.Repeat(CompletionItemSource.AsKeyword, 1), applicableSpan);
                    }

                    if (context.Position >= name.StartIndex) {
                        var applicableSpan = name.GetSpan(context.Ast);
                        var mods = (await GetModulesFromDottedNameAsync(fromImport.Root, context, cancellationToken)).ToArray();
                        result = mods.Any() && fromImport.Names.Count == 1 ? Enumerable.Repeat(CompletionItemSource.Star, 1).Concat(mods) : mods;
                        return true;
                    }
                }
            }

            if (fromImport.ImportIndex > fromImport.StartIndex) {
                if (context.Position > fromImport.ImportIndex + 6 && Analysis.Scope.AnalysisValue is ModuleInfo moduleInfo) {
                    var mres = context.Analysis.Document.Interpreter.ModuleResolution;
                    var importSearchResult = mres.CurrentPathResolver.FindImports(moduleInfo.ProjectEntry.FilePath, fromImport);
                    switch (importSearchResult) {
                        case ModuleImport moduleImports when moduleInfo.TryGetModuleReference(moduleImports.FullName, out var moduleReference):
                        case PossibleModuleImport possibleModuleImport when moduleInfo.TryGetModuleReference(possibleModuleImport.PossibleModuleFullName, out moduleReference):
                            var module = moduleReference.Module;
                            if (module != null) {
                                result = module.GetAllMembers(Analysis.InterpreterContext)
                                    .GroupBy(kvp => kvp.Key)
                                    .Select(g => (IMemberResult)new MemberResult(g.Key, g.SelectMany(kvp => kvp.Value)))
                                    .Select(ToCompletionItem)
                                    .Prepend(StarCompletion);
                            }

                            return true;

                        case PackageImport packageImports:
                            var modules = Analysis.ProjectState.Modules;
                            result = packageImports.Modules
                                .Select(m => {
                                    var hasReference = modules.TryGetImportedModule(m.FullName, out var mr);
                                    return (hasReference: hasReference && mr?.Module != null, name: m.Name, module: mr?.AnalysisModule);
                                })
                                .Where(t => t.hasReference)
                                .Select(t => ToCompletionItem(new MemberResult(t.name, new[] { t.module })))
                                .Prepend(CompletionItemSource.Star);
                            return true;
                        default:
                            return true;
                    }
                }

                if (context.Position >= fromImport.ImportIndex) {
                    var applicableSpan = new SourceSpan(
                        context.IndexToLocation(fromImport.ImportIndex),
                        context.IndexToLocation(Math.Min(fromImport.ImportIndex + 6, fromImport.EndIndex))
                    );
                    result = new CompletionResult(Enumerable.Repeat(CompletionItemSource.ImportKeyword, 1), applicableSpan);
                    return true;
                }
            }

            if (context.Position > fromImport.Root.EndIndex && fromImport.Root.EndIndex > fromImport.Root.StartIndex) {
                SourceSpan applicableSpan = default;
                if (context.Position > fromImport.EndIndex) {
                    // Only end up here for "from ... imp", and "imp" is not counted
                    // as part of our span
                    var token = context.TokenSource.Tokens.LastOrDefault();
                    if (token.Key.End >= context.Position) {
                        applicableSpan = context.TokenSource.GetTokenSpan(token.Key);
                    }
                }

                result = new CompletionResult(Enumerable.Repeat(CompletionItemSource.ImportKeyword, 1), applicableSpan);
                return true;
            }

            if (context.Position >= fromImport.Root.StartIndex) {
                Node = fromImport.Root.Names.MaybeEnumerate()
                    .LastOrDefault(n => n.StartIndex <= context.Position && context.Position <= n.EndIndex);
                result = GetModulesFromNode(fromImport.Root);
                return true;
            }

            if (context.Position > fromImport.KeywordEndIndex) {
                result = GetModules();
                return true;
            }

            return false;
        }

        private static async Task<IEnumerable<CompletionItem>> GetModulesAsync(CompletionContext context, CancellationToken cancellationToken = default) {
            var modules = await context.Analysis.Document.Interpreter.ModuleResolution.GetImportableModulesAsync(cancellationToken);
            return modules.Select(kvp => CompletionItemSource.CreateCompletionItem(kvp.Key, CompletionItemKind.Module));
        }

        private static Task<IEnumerable<CompletionItem>> GetModulesFromDottedNameAsync(DottedName name, CompletionContext context, CancellationToken cancellationToken = default)
            => GetModulesFromDottedPathAsync(GetPathFromDottedName(name, context), context, cancellationToken);

        private static async Task<IEnumerable<CompletionItem>> GetModulesFromDottedPathAsync(string[] pathItems, CompletionContext context, CancellationToken cancellationToken = default) {
            if (pathItems.Any()) {
                return Analysis.ProjectState
                    .GetModuleMembers(Analysis.InterpreterContext, names, includeMembers)
                    .Select(ToCompletionItem);
            }

            return await GetModulesAsync(context, cancellationToken);
        }

        private static string[] GetPathFromDottedName(DottedName name, CompletionContext context)
            => name.Names.TakeWhile(n => context.Position > n.EndIndex).Select(n => n.Name).ToArray();

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
