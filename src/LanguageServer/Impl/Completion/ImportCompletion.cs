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
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Completion {
    internal class ImportCompletion {
        public static CompletionResult TryGetCompletions(ImportStatement import, CompletionContext context) {
            // No names, so if we are at the end. Return available modules
            if (import.Names.Count == 0 && context.Position > import.KeywordEndIndex) {
                return new CompletionResult(GetAllImportableModules(context));
            }

            for (var i = import.Names.Count - 1; i >= 0; i--) {
                if (import.AsNames.Count > i && import.AsNames[i] != null && context.Position >= import.AsNames[i].StartIndex) {
                    return CompletionResult.Empty;
                }

                var name = import.Names[i];
                if (name != null && context.Position >= name.StartIndex) {
                    if (context.Position > name.EndIndex && name.EndIndex > name.StartIndex) {
                        var applicableSpan = context.GetApplicableSpanFromLastToken(import);
                        return new CompletionResult(new[] { CompletionItemSource.AsKeyword }, applicableSpan);
                    }

                    if (name.Names.Count == 0 || name.Names[0].EndIndex >= context.Position) {
                        return new CompletionResult(GetAllImportableModules(context));
                    }

                    var document = context.Analysis.Document;
                    var mres = document.Interpreter.ModuleResolution;
                    var names = name.Names.TakeWhile(n => n.EndIndex < context.Position).Select(n => n.Name);
                    var importSearchResult = mres.CurrentPathResolver.GetImportsFromAbsoluteName(document.FilePath, names, import.ForceAbsolute);
                    return GetResultFromImportSearch(importSearchResult, context, false, modulesOnly: true);
                }
            }
            return null;
        }

        public static CompletionResult GetCompletionsInFromImport(FromImportStatement fromImport, CompletionContext context) {
            // No more completions after '*', ever!
            if (fromImport.Names.Any(n => n?.Name == "*" && context.Position > n.EndIndex)) {
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
                        var importSearchResult = mres.CurrentPathResolver.FindImports(document.FilePath, fromImport);
                        return GetResultFromImportSearch(importSearchResult, context, false, applicableSpan);
                    }
                }
            }

            if (fromImport.ImportIndex > fromImport.StartIndex && context.Position > fromImport.ImportIndex + 6) {
                var importSearchResult = mres.CurrentPathResolver.FindImports(document.FilePath, fromImport);
                var result = GetResultFromImportSearch(importSearchResult, context, true);
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

            if (context.Position > fromImport.Root.StartIndex && fromImport.Root is RelativeModuleName relativeName) {
                var rootNames = relativeName.Names.Select(n => n.Name);
                var importSearchResult = mres.CurrentPathResolver.GetImportsFromRelativePath(document.FilePath, relativeName.DotCount, rootNames);
                return GetResultFromImportSearch(importSearchResult, context, false);
            }

            if (fromImport.Root.Names.Count > 1 && context.Position > fromImport.Root.Names[0].EndIndex) {
                var rootNames = fromImport.Root.Names.TakeWhile(n => n.EndIndex < context.Position).Select(n => n.Name);
                var importSearchResult = mres.CurrentPathResolver.GetImportsFromAbsoluteName(document.FilePath, rootNames, fromImport.ForceAbsolute);
                return GetResultFromImportSearch(importSearchResult, context, false);
            }

            return context.Position > fromImport.KeywordEndIndex
                ? new CompletionResult(GetAllImportableModules(context))
                : null;
        }

        private static IEnumerable<CompletionItem> GetAllImportableModules(CompletionContext context) {
            var interpreter = context.Analysis.Document.Interpreter;
            var languageVersion = interpreter.LanguageVersion.ToVersion();
            var includeImplicit = !ModulePath.PythonVersionRequiresInitPyFiles(languageVersion);
            var modules = interpreter.ModuleResolution.CurrentPathResolver.GetAllImportableModuleNames(includeImplicit);
            return modules
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct()
                .Select(n => CompletionItemSource.CreateCompletionItem(n, CompletionItemKind.Module));
        }

        private static CompletionResult GetResultFromImportSearch(IImportSearchResult importSearchResult, CompletionContext context, bool prependStar, SourceSpan? applicableSpan = null, bool modulesOnly = false) {
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
                case ImplicitPackageImport _:
                    module = null;
                    break;
                default:
                    return CompletionResult.Empty;
            }

            var completions = new List<CompletionItem>();
            if (prependStar) {
                completions.Add(CompletionItemSource.Star);
            }

            var memberNames = (module?.GetMemberNames().Where(n => !string.IsNullOrEmpty(n)) ?? Enumerable.Empty<string>()).ToHashSet();
            if (module != null) {
                var moduleMembers = memberNames
                    .Select(n => (n, m: module.GetMember(n)))
                    .Where(pair => !modulesOnly || pair.m is IPythonModule)
                    .Select(pair => context.ItemSource.CreateCompletionItem(pair.n, pair.m));
                completions.AddRange(moduleMembers);
            }

            if (importSearchResult is IImportChildrenSource children) {
                foreach (var childName in children.GetChildrenNames()) {
                    if (!children.TryGetChildImport(childName, out var imports)) {
                        continue;
                    }

                    string name = null;
                    switch (imports) {
                        case ImplicitPackageImport packageImport:
                            name = packageImport.Name;
                            break;
                        case ModuleImport moduleImport when !moduleImport.ModulePath.PathEquals(document.FilePath):
                            name = moduleImport.Name;
                            break;
                    }

                    if (name != null && !completions.Any(c => c.label == name && c.kind == CompletionItemKind.Module)) {
                        completions.Add(CompletionItemSource.CreateCompletionItem(name, CompletionItemKind.Module));
                    }
                }
            }

            return new CompletionResult(completions, applicableSpan);
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
