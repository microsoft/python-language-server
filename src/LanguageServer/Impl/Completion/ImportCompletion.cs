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
using System.IO;
using System.Linq;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Completion {
    internal class ImportCompletion {
        public static CompletionResult TryGetCompletions(ImportStatement import, CompletionContext context) {
            // No names, so if we are at the end. Return available modules
            if (import.Names.Count == 0 && context.Position > import.KeywordEndIndex) {
                return new CompletionResult(GetAllImportableModules(context));
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

                    return new CompletionResult(GetImportsFromModuleName(name.Names, context));
                }
            }
            return null;
        }

        public static CompletionResult GetCompletionsInFromImport(FromImportStatement fromImport, CompletionContext context) {
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
                        return new CompletionResult(GetModuleMembers(fromImport.Root.Names, context), applicableSpan);
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

            if (fromImport.ImportIndex > 0 && context.Position >= fromImport.ImportIndex) {
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
                return new CompletionResult(GetImportsFromModuleName(fromImport.Root.Names, context));
            }

            return context.Position > fromImport.KeywordEndIndex
                ? new CompletionResult(GetAllImportableModules(context))
                : null;
        }

        private static IEnumerable<CompletionItem> GetImportsFromModuleName(IEnumerable<NameExpression> nameExpressions, CompletionContext context) {
            var names = nameExpressions.TakeWhile(n => n.StartIndex <= context.Position).Select(n => n.Name).ToArray();
            return names.Length <= 1 ? GetAllImportableModules(context) : GetChildModules(names, context);
        }

        private static IEnumerable<CompletionItem> GetModuleMembers(IEnumerable<NameExpression> nameExpressions, CompletionContext context) {
            var fullName = string.Join(".", nameExpressions.Select(n => n.Name));
            var mres = context.Analysis.Document.Interpreter.ModuleResolution;

            var module = mres.GetImportedModule(fullName);
            return module != null 
                ? module.GetMemberNames().Select(n => context.ItemSource.CreateCompletionItem(n, module.GetMember(n))) 
                : Enumerable.Empty<CompletionItem>();
        }

        private static IEnumerable<CompletionItem> GetAllImportableModules(CompletionContext context) {
            var mres = context.Analysis.Document.Interpreter.ModuleResolution;
            var modules = mres.CurrentPathResolver.GetAllModuleNames().Distinct();
            return modules.Select(n => CompletionItemSource.CreateCompletionItem(n, CompletionItemKind.Module));
        }

        private static CompletionResult GetResultFromSearch(IImportSearchResult importSearchResult, CompletionContext context) {
            var document = context.Analysis.Document;
            var mres = document.Interpreter.ModuleResolution;

            IPythonModule module;
            switch (importSearchResult) {
                case ModuleImport moduleImports:
                    module = mres.GetImportedModule(moduleImports.FullName);
                    break;
                case PossibleModuleImport possibleModuleImport:
                    module = mres.GetImportedModule(possibleModuleImport.PossibleModuleFullName);
                    break;
                case PackageImport packageImports:
                    return new CompletionResult(packageImports.Modules
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

        private static IReadOnlyList<CompletionItem> GetChildModules(string[] names, CompletionContext context) {
            if (!names.Any()) {
                return Array.Empty<CompletionItem>();
            }

            var mres = context.Analysis.Document.Interpreter.ModuleResolution;
            var fullName = string.Join(".", names.Take(names.Length - 1));

            var import = mres.CurrentPathResolver.GetModuleImportFromModuleName(fullName);
            if (string.IsNullOrEmpty(import?.ModulePath)) {
                return Array.Empty<CompletionItem>();
            }

            var packages = mres.GetPackagesFromDirectory(Path.GetDirectoryName(import.ModulePath));
            return packages.Select(n => CompletionItemSource.CreateCompletionItem(n, CompletionItemKind.Module)).ToArray();
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
