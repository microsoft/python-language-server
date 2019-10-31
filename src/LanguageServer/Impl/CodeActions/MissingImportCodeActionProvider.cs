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
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Analyzer.Expressions;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Diagnostics;
using Microsoft.Python.LanguageServer.Indexing;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.LanguageServer.Utilities;
using Microsoft.Python.Parsing.Ast;
using Range = Microsoft.Python.Core.Text.Range;

namespace Microsoft.Python.LanguageServer.CodeActions {
    internal sealed class MissingImportCodeActionProvider : ICodeActionProvider {
        public static readonly ICodeActionProvider Instance = new MissingImportCodeActionProvider();

        // right now, it is a static. in future, we might consider giving an option to users to customize this list
        // also, right now, it is text based. so if module has same name, they will get same suggestion even if
        // the module is not something user expected
        private static readonly Dictionary<string, string> WellKnownAbbreviationMap = new Dictionary<string, string>() {
            { "numpy", "np" },
            { "pandas", "pd" },
            { "tensorflow", "tf" },
            { "matplotlib.pyplot", "plt" },
            { "matplotlib", "mpl" },
            { "math", "m" },
            { "scipy.io", "spio" },
            { "scipy", "sp" },
        };

        private MissingImportCodeActionProvider() {
        }

        public ImmutableArray<string> FixableDiagnostics => ImmutableArray<string>.Create(
            ErrorCodes.UndefinedVariable, ErrorCodes.VariableNotDefinedGlobally, ErrorCodes.VariableNotDefinedNonLocal);

        public async Task<IEnumerable<CodeAction>> GetCodeActionsAsync(IDocumentAnalysis analysis, DiagnosticsEntry diagnostic, CancellationToken cancellationToken) {
            var finder = new ExpressionFinder(analysis.Ast, new FindExpressionOptions() { Names = true });
            var node = finder.GetExpression(diagnostic.SourceSpan);
            if (!(node is NameExpression nex)) {
                return Enumerable.Empty<CodeAction>();
            }

            var identifier = nex.Name;
            if (string.IsNullOrEmpty(identifier)) {
                return Enumerable.Empty<CodeAction>();
            }

            var codeActions = new List<CodeAction>();
            var diagnostics = new[] { diagnostic.ToDiagnostic() };

            // see whether it is one of abbreviation we specialize
            foreach (var moduleFullName in WellKnownAbbreviationMap.Where(kv => kv.Value == identifier).Select(kv => kv.Key)) {
                var moduleName = GetModuleName(moduleFullName);

                await GetCodeActionsAsync(analysis, diagnostics, new Input(node, moduleName, moduleFullName), codeActions, cancellationToken);
            }

            // add then search given name as it is
            await GetCodeActionsAsync(analysis, diagnostics, new Input(node, identifier), codeActions, cancellationToken);

            return codeActions;

            string GetModuleName(string moduleFullName) {
                var index = moduleFullName.LastIndexOf(".");
                return index < 0 ? moduleFullName : moduleFullName.Substring(index + 1);
            }
        }

        private async Task GetCodeActionsAsync(IDocumentAnalysis analysis,
                                               Diagnostic[] diagnostics,
                                               Input input,
                                               List<CodeAction> codeActions,
                                               CancellationToken cancellationToken) {
            var importFullNameMap = new Dictionary<string, ImportInfo>();
            await AddCandidatesFromIndexAsync(analysis, input.Identifier, importFullNameMap, cancellationToken);

            var interpreter = analysis.Document.Interpreter;
            var pathResolver = interpreter.ModuleResolution.CurrentPathResolver;

            // find installed modules matching the given name. this will include submodules
            var languageVersion = Parsing.PythonLanguageVersionExtensions.ToVersion(interpreter.LanguageVersion);
            var includeImplicit = !ModulePath.PythonVersionRequiresInitPyFiles(languageVersion);

            foreach (var moduleFullName in pathResolver.GetAllImportableModulesByName(input.Identifier, includeImplicit)) {
                cancellationToken.ThrowIfCancellationRequested();
                importFullNameMap[moduleFullName] = new ImportInfo(moduleImported: false, memberImported: false, isModule: true);
            }

            // find members matching the given name from modules already loaded.
            var moduleInfo = new ModuleInfo(analysis);
            foreach (var module in interpreter.ModuleResolution.GetImportedModules(cancellationToken)) {
                if (module.ModuleType == ModuleType.Unresolved) {
                    continue;
                }

                // module name is full module name that you can use in import xxxx directly
                CollectCandidates(moduleInfo.Reset(module), input.Identifier, importFullNameMap, cancellationToken);
                Debug.Assert(moduleInfo.NameParts.Count == 1 && moduleInfo.NameParts[0] == module.Name);
            }

            // check quick bail out case where we know what module we are looking for
            if (input.ModuleFullNameOpt != null) {
                if (importFullNameMap.ContainsKey(input.ModuleFullNameOpt)) {
                    // add code action if the module exist, otherwise, bail out empty
                    codeActions.AddIfNotNull(CreateCodeAction(analysis, input.Context, input.ModuleFullNameOpt, diagnostics, locallyInserted: false, cancellationToken));
                }
                return;
            }

            // regular case
            FilterCandidatesBasedOnContext(analysis, input.Context, importFullNameMap, cancellationToken);

            // this will create actual code fix with certain orders
            foreach (var fullName in OrderFullNames(importFullNameMap)) {
                cancellationToken.ThrowIfCancellationRequested();
                codeActions.AddIfNotNull(CreateCodeAction(analysis, input.Context, fullName, diagnostics, locallyInserted: false, cancellationToken));
            }
        }

        private void FilterCandidatesBasedOnContext(IDocumentAnalysis analysis, Node node, Dictionary<string, ImportInfo> importFullNameMap, CancellationToken cancellationToken) {
            var ancestors = GetAncestorsOrThis(analysis.Ast.Body, node, cancellationToken);
            var index = ancestors.LastIndexOf(node);
            if (index <= 0) {
                // nothing to filter on
                return;
            }

            var parent = ancestors[index - 1];
            if (!(parent is CallExpression)) {
                // nothing to filter on
                return;
            }

            // do simple filtering
            // remove all modules from candidates
            foreach (var kv in importFullNameMap.ToList()) {
                if (kv.Value.IsModule) {
                    importFullNameMap.Remove(kv.Key);
                }
            }
        }

        private IEnumerable<string> OrderFullNames(Dictionary<string, ImportInfo> importFullNameMap) {
            // use some heuristic to improve code fix ordering

            // put simple name module at the top
            foreach (var fullName in OrderImportNames(importFullNameMap.Where(FilterSimpleName).Select(kv => kv.Key))) {
                importFullNameMap.Remove(fullName);
                yield return fullName;
            }

            // heuristic is we put entries with decl without any exports (imported member with __all__) at the top
            // such as array. another example will be chararray. 
            // this will make numpy chararray at the top and numpy defchararray at the bottom.
            // if we want, we can add more info to hide intermediate ones. 
            // for example, numpy.chararry is __all__.extended from numpy.core.chararray and etc.
            // so we could leave only numpy.chararray and remove ones like numpy.core.chararray and etc. but for now,
            // we show all those but in certain order so that numpy.chararray shows up top
            // this heuristic still has issue with something like os.path.join since no one import macpath, macpath join shows up high
            var sourceDeclarationFullNames = importFullNameMap.Where(kv => kv.Value.Symbol != null)
                                                              .GroupBy(kv => kv.Value.Symbol.Definition, LocationInfo.FullComparer)
                                                              .Where(FilterSourceDeclarations)
                                                              .Select(g => g.First().Key);

            foreach (var fullName in OrderImportNames(sourceDeclarationFullNames)) {
                importFullNameMap.Remove(fullName);
                yield return fullName;
            }

            // put modules that are imported next
            foreach (var fullName in OrderImportNames(importFullNameMap.Where(FilterImportedModules).Select(kv => kv.Key))) {
                importFullNameMap.Remove(fullName);
                yield return fullName;
            }

            // put members that are imported next
            foreach (var fullName in OrderImportNames(importFullNameMap.Where(FilterImportedMembers).Select(kv => kv.Key))) {
                importFullNameMap.Remove(fullName);
                yield return fullName;
            }

            // put members whose module is imported next
            foreach (var fullName in OrderImportNames(importFullNameMap.Where(FilterImportedModuleMembers).Select(kv => kv.Key))) {
                importFullNameMap.Remove(fullName);
                yield return fullName;
            }

            // put things left here.
            foreach (var fullName in OrderImportNames(importFullNameMap.Select(kv => kv.Key))) {
                yield return fullName;
            }

            List<string> OrderImportNames(IEnumerable<string> fullNames) {
                return fullNames.OrderBy(n => n, ImportNameComparer.Instance).ToList();
            }

            bool FilterSimpleName(KeyValuePair<string, ImportInfo> kv) => kv.Key.IndexOf(".") < 0;
            bool FilterImportedMembers(KeyValuePair<string, ImportInfo> kv) => !kv.Value.IsModule && kv.Value.MemberImported;
            bool FilterImportedModuleMembers(KeyValuePair<string, ImportInfo> kv) => !kv.Value.IsModule && kv.Value.ModuleImported;
            bool FilterImportedModules(KeyValuePair<string, ImportInfo> kv) => kv.Value.IsModule && kv.Value.MemberImported;

            bool FilterSourceDeclarations(IGrouping<LocationInfo, KeyValuePair<string, ImportInfo>> group) {
                var count = 0;
                foreach (var entry in group) {
                    if (count++ > 0) {
                        return false;
                    }

                    var value = entry.Value;
                    if (value.ModuleImported || value.ModuleImported) {
                        return false;
                    }
                }

                return true;
            }
        }

        private static async Task AddCandidatesFromIndexAsync(IDocumentAnalysis analysis,
                                                             string name,
                                                             Dictionary<string, ImportInfo> importFullNameMap,
                                                             CancellationToken cancellationToken) {
            var indexManager = analysis.ExpressionEvaluator.Services.GetService<IIndexManager>();
            if (indexManager == null) {
                // indexing is not supported
                return;
            }

            var symbolsIncludingName = await indexManager.WorkspaceSymbolsAsync(name, maxLength: int.MaxValue, includeLibraries: true, cancellationToken);

            // we only consider exact matches rather than partial matches
            var symbolsWithName = symbolsIncludingName.Where(Include);

            var analyzer = analysis.ExpressionEvaluator.Services.GetService<IPythonAnalyzer>();
            var pathResolver = analysis.Document.Interpreter.ModuleResolution.CurrentPathResolver;

            var modules = ImmutableArray<IPythonModule>.Empty;
            foreach (var symbolAndModuleName in symbolsWithName.Select(s => (symbol: s, moduleName: pathResolver.GetModuleNameByPath(s.DocumentPath)))) {
                cancellationToken.ThrowIfCancellationRequested();

                var key = $"{symbolAndModuleName.moduleName}.{symbolAndModuleName.symbol.Name}";
                var symbol = symbolAndModuleName.symbol;

                importFullNameMap.TryGetValue(key, out var existing);

                // we don't actually know whether this is a module. all we know is it appeared at
                // Import statement. but most likely module, so we mark it as module for now.
                // later when we check loaded module, if this happen to be loaded, this will get
                // updated with more accurate data.
                // if there happen to be multiple symbols with same name, we refer to mark it as module
                var isModule = symbol.Kind == Indexing.SymbolKind.Module || existing.IsModule;

                // any symbol marked "Module" by indexer is imported.
                importFullNameMap[key] = new ImportInfo(
                    moduleImported: isModule,
                    memberImported: isModule,
                    isModule);
            }

            bool Include(FlatSymbol symbol) {
                // we only suggest symbols that exist in __all__
                // otherwise, we show gigantic list from index
                return symbol._existInAllVariable &&
                       symbol.ContainerName == null &&
                       CheckKind(symbol.Kind) &&
                       symbol.Name == name;
            }

            bool CheckKind(Indexing.SymbolKind kind) {
                switch (kind) {
                    case Indexing.SymbolKind.Module:
                    case Indexing.SymbolKind.Namespace:
                    case Indexing.SymbolKind.Package:
                    case Indexing.SymbolKind.Class:
                    case Indexing.SymbolKind.Enum:
                    case Indexing.SymbolKind.Interface:
                    case Indexing.SymbolKind.Function:
                    case Indexing.SymbolKind.Constant:
                    case Indexing.SymbolKind.Struct:
                        return true;
                    default:
                        return false;
                }
            }
        }

        private CodeAction CreateCodeAction(IDocumentAnalysis analysis,
                                            Node node,
                                            string moduleFullName,
                                            Diagnostic[] diagnostics,
                                            bool locallyInserted,
                                            CancellationToken cancellationToken) {
            var insertionPoint = GetInsertionInfo(analysis, node, moduleFullName, locallyInserted, cancellationToken);
            if (insertionPoint == null) {
                return null;
            }

            var insertionText = insertionPoint.Value.InsertionText;
            var titleText = locallyInserted ? Resources.ImportLocally.FormatUI(insertionText) : insertionText;

            var sb = new StringBuilder();
            sb.AppendIf(insertionPoint.Value.Range.start == insertionPoint.Value.Range.end, insertionPoint.Value.Indentation);
            sb.Append(insertionPoint.Value.AddBlankLine ? insertionText + Environment.NewLine : insertionText);
            sb.AppendIf(insertionPoint.Value.Range.start == insertionPoint.Value.Range.end, Environment.NewLine);

            var textEdits = new List<TextEdit>();
            textEdits.Add(new TextEdit() { range = insertionPoint.Value.Range, newText = sb.ToString() });

            if (insertionPoint.Value.AbbreviationOpt != null) {
                textEdits.Add(new TextEdit() { range = node.GetSpan(analysis.Ast), newText = insertionPoint.Value.AbbreviationOpt });
            }

            var changes = new Dictionary<Uri, TextEdit[]> { { analysis.Document.Uri, textEdits.ToArray() } };
            return new CodeAction() { title = titleText, kind = CodeActionKind.QuickFix, diagnostics = diagnostics, edit = new WorkspaceEdit() { changes = changes } };
        }

        private InsertionInfo? GetInsertionInfo(IDocumentAnalysis analysis,
                                                Node node,
                                                string fullyQualifiedName,
                                                bool locallyInserted,
                                                CancellationToken cancellationToken) {
            var (body, indentation) = GetStartingPoint(analysis, node, locallyInserted, cancellationToken);
            if (body == null) {
                // no insertion point
                return null;
            }

            var importNodes = body.GetChildNodes().Where(c => c is ImportStatement || c is FromImportStatement).ToList();
            var lastImportNode = importNodes.LastOrDefault();

            var abbreviation = GetAbbreviationForWellKnownModules(analysis, fullyQualifiedName);

            // first check whether module name is dotted or not
            var dotIndex = fullyQualifiedName.LastIndexOf('.');
            if (dotIndex < 0) {
                // there can't be existing import since we have the error
                return new InsertionInfo(addBlankLine: lastImportNode == null,
                                         GetInsertionText($"import {fullyQualifiedName}", abbreviation),
                                         GetRange(analysis.Ast, body, lastImportNode),
                                         indentation,
                                         abbreviation);
            }

            // see whether there is existing from * import * statement.
            var fromPart = fullyQualifiedName.Substring(startIndex: 0, dotIndex);
            var nameToAdd = fullyQualifiedName.Substring(dotIndex + 1);
            foreach (var current in importNodes.Reverse<Node>().OfType<FromImportStatement>()) {
                if (current.Root.MakeString() == fromPart) {
                    return new InsertionInfo(addBlankLine: false,
                                             GetInsertionText(current, fromPart, nameToAdd, abbreviation),
                                             current.GetSpan(analysis.Ast),
                                             indentation,
                                             abbreviation);
                }
            }

            // add new from * import * statement
            return new InsertionInfo(addBlankLine: lastImportNode == null,
                                     GetInsertionText($"from {fromPart} import {nameToAdd}", abbreviation),
                                     GetRange(analysis.Ast, body, lastImportNode),
                                     indentation,
                                     abbreviation);
        }

        private static string GetAbbreviationForWellKnownModules(IDocumentAnalysis analysis, string fullyQualifiedName) {
            if (WellKnownAbbreviationMap.TryGetValue(fullyQualifiedName, out var abbreviation)) {
                // for now, use module wide unique name for abbreviation. even though technically we could use
                // context based unique name since variable declared in lower scope will hide it and there is no conflict
                return UniqueNameGenerator.Generate(analysis, abbreviation);
            }

            return null;
        }

        private static string GetInsertionText(string insertionText, string abbreviation) =>
            abbreviation == null ? insertionText : $"{insertionText} as {abbreviation}";

        private string GetInsertionText(FromImportStatement fromImportStatement, string rootModuleName, string moduleNameToAdd, string abbreviation) {
            var imports = fromImportStatement.Names.Select(n => n.Name)
                .Concat(new string[] { GetInsertionText(moduleNameToAdd, abbreviation) })
                .OrderBy(n => n).ToList();

            return $"from {rootModuleName} import {string.Join(", ", imports)}";
        }

        private Range GetRange(PythonAst ast, Statement body, Node lastImportNode) {
            var position = GetPosition(ast, body, lastImportNode);
            return new Range() { start = position, end = position };
        }

        private Position GetPosition(PythonAst ast, Statement body, Node lastImportNode) {
            if (lastImportNode != null) {
                var endLocation = lastImportNode.GetEnd(ast);
                return new Position { line = endLocation.Line, character = 0 };
            }

            // firstNode must exist in this context
            var firstNode = body.GetChildNodes().First();
            return new Position() { line = firstNode.GetStart(ast).Line - 1, character = 0 };
        }

        private (Statement body, string indentation) GetStartingPoint(IDocumentAnalysis analysis,
                                                                      Node node,
                                                                      bool locallyInserted,
                                                                      CancellationToken cancellationToken) {
            if (!locallyInserted) {
                return (analysis.Ast.Body, string.Empty);
            }

            var candidate = GetAncestorsOrThis(analysis.Ast.Body, node, cancellationToken).Where(p => p is FunctionDefinition).LastOrDefault();

            // for now, only stop at FunctionDefinition. 
            // we can expand it to more scope if we want but this seems what other tool also provide as well.
            // this will return closest scope from given node
            switch (candidate) {
                case FunctionDefinition functionDefinition:
                    return (functionDefinition.Body, GetIndentation(analysis.Ast, functionDefinition.Body));
                default:
                    // no local scope
                    return default;
            }
        }

        private string GetIndentation(PythonAst ast, Statement body) {
            // first token must exist in current context
            var firstToken = body.GetChildNodes().First();

            // not sure how to handle a case where user is using "tab" instead of "space"
            // for indentation. where can one get tab over indentation option?
            return new string(' ', firstToken.GetStart(ast).Column - 1);
        }

        private List<Node> GetAncestorsOrThis(Node root, Node node, CancellationToken cancellationToken) {
            var parentChain = new List<Node>();

            // there seems no way to go up the parent chain. always has to go down from the top
            while (root != null) {
                cancellationToken.ThrowIfCancellationRequested();

                var temp = root;
                root = null;

                // this assumes node is not overlapped and children are ordered from left to right
                // in textual position
                foreach (var current in temp.GetChildNodes()) {
                    if (!current.IndexSpan.Contains(node.IndexSpan)) {
                        continue;
                    }

                    parentChain.Add(current);
                    root = current;
                    break;
                }
            }

            return parentChain;
        }

        private void CollectCandidates(ModuleInfo moduleInfo,
                                       string name,
                                       Dictionary<string, ImportInfo> importFullNameMap,
                                       CancellationToken cancellationToken) {
            if (!moduleInfo.CheckCircularImports()) {
                // bail out on circular imports
                return;
            }

            // add non module (imported) member
            AddNonImportedMemberWithName(moduleInfo, name, importFullNameMap);

            // add module (imported) members if it shows up in __all__
            //
            // we are doing recursive dig down rather than just going through all modules loaded linearly
            // since path to how to get to a module is important.
            // for example, "join" is defined in "ntpath" or "macpath" and etc, but users are supposed to
            // use it through "os.path" which will automatically point to right module ex, "ntpath" based on
            // environment rather than "ntpath" directly. if we just go through module in flat list, then
            // we can miss "os.path" since it won't show in the module list.
            // for these modules that are supposed to be used with indirect path (imported name of the module),
            // we need to dig down to collect those with right path.
            foreach (var memberName in GetAllVariables(moduleInfo.Analysis)) {
                cancellationToken.ThrowIfCancellationRequested();

                var pythonModule = moduleInfo.Module.GetMember(memberName) as IPythonModule;
                if (pythonModule == null) {
                    continue;
                }

                var fullName = $"{moduleInfo.FullName}.{memberName}";
                if (string.Equals(memberName, name)) {
                    // nested module are all imported
                    AddNameParts(fullName, moduleImported: true, memberImported: true, pythonModule, importFullNameMap);
                }

                // make sure we dig down modules only if we can use it from imports
                // for example, user can do "from numpy import char" to import char [defchararray] module
                // but user can not do "from numpy.char import x" since it is not one of known modules to us.
                // in contrast, users can do "from os import path" to import path [ntpath] module
                // but also can do "from os.path import x" since "os.path" is one of known moudles to us.
                var result = AstUtilities.FindImports(
                    moduleInfo.CurrentFileAnalysis.Document.Interpreter.ModuleResolution.CurrentPathResolver,
                    moduleInfo.CurrentFileAnalysis.Document.FilePath,
                    GetRootNames(fullName),
                    dotCount: 0,
                    forceAbsolute: true);

                if (result is ImportNotFound) {
                    continue;
                }

                moduleInfo.AddName(memberName);
                CollectCandidates(moduleInfo.With(pythonModule), name, importFullNameMap, cancellationToken);
                moduleInfo.PopName();
            }

            // pop this module out so we can get to this module from
            // different path. 
            // ex) A -> B -> [C] and A -> D -> [C]
            moduleInfo.ForgetModule();
        }

        private IEnumerable<string> GetRootNames(string fullName) {
            return fullName.Split('.');
        }

        private void AddNonImportedMemberWithName(ModuleInfo moduleInfo, string name, Dictionary<string, ImportInfo> importFullNameMap) {
            // for now, skip any protected or private member
            if (name.StartsWith("_")) {
                return;
            }

            var pythonType = moduleInfo.Module.GetMember<IPythonType>(name);
            if (pythonType == null || pythonType is IPythonModule || pythonType.IsUnknown()) {
                return;
            }

            // skip any imported member (non module member) unless it is explicitly on __all__
            if (moduleInfo.Analysis.GlobalScope.Imported.TryGetVariable(name, out var importedVariable) &&
                object.Equals(pythonType, importedVariable.Value) &&
                GetAllVariables(moduleInfo.Analysis).All(s => !string.Equals(s, name))) {
                return;
            }

            moduleInfo.AddName(name);
            AddNameParts(moduleInfo.FullName, moduleInfo.ModuleImported, importedVariable != null, pythonType, importFullNameMap);
            moduleInfo.PopName();
        }

        private static void AddNameParts(
            string fullName, bool moduleImported, bool memberImported, IPythonType symbol, Dictionary<string, ImportInfo> moduleFullNameMap) {
            // one of case this can happen is if module's fullname is "a.b.c" and module "a.b" also import module "a.b.c" as "c" making
            // fullname same "a.b.c". in this case, we mark it as "imported" since we refer one explicily shown in "__all__" to show
            // higher rank than others
            if (moduleFullNameMap.TryGetValue(fullName, out var info)) {
                moduleImported |= info.ModuleImported;
            }

            moduleFullNameMap[fullName] = new ImportInfo(moduleImported, memberImported, symbol);
        }

        private IEnumerable<string> GetAllVariables(IDocumentAnalysis analysis) {
            // this is different than StartImportMemberNames since that only returns something when
            // all entries are known. for import, we are fine doing best effort
            if (analysis.GlobalScope.Variables.TryGetVariable("__all__", out var variable) &&
                variable?.Value is IPythonCollection collection) {
                return collection.Contents
                    .OfType<IPythonConstant>()
                    .Select(c => c.GetString())
                    .Where(s => !string.IsNullOrEmpty(s));
            }

            return Array.Empty<string>();
        }

        private class ImportNameComparer : IComparer<string> {
            public static readonly ImportNameComparer Instance = new ImportNameComparer();

            private ImportNameComparer() { }

            public int Compare(string x, string y) {
                const string underscore = "_";

                // move "_" to back of the list
                if (x.StartsWith(underscore) && y.StartsWith(underscore)) {
                    return x.CompareTo(y);
                }
                if (x.StartsWith(underscore)) {
                    return 1;
                }
                if (y.StartsWith(underscore)) {
                    return -1;
                }

                return x.CompareTo(y);
            }
        }

        private struct InsertionInfo {
            public readonly bool AddBlankLine;
            public readonly string InsertionText;
            public readonly Range Range;
            public readonly string Indentation;
            public readonly string AbbreviationOpt;

            public InsertionInfo(bool addBlankLine, string insertionText, Range range, string indentation, string abbreviationOpt = null) {
                AddBlankLine = addBlankLine;
                InsertionText = insertionText;
                Range = range;
                Indentation = indentation;
                AbbreviationOpt = abbreviationOpt;
            }
        }

        private struct Input {
            public readonly Node Context;
            public readonly string Identifier;
            public readonly string ModuleFullNameOpt;

            public Input(Node context, string identifier, string moduleFullNameOpt = null) {
                Context = context;
                Identifier = identifier;
                ModuleFullNameOpt = moduleFullNameOpt;
            }
        }

        private struct ModuleInfo {
            public readonly IDocumentAnalysis CurrentFileAnalysis;
            public readonly IPythonModule Module;
            public readonly List<string> NameParts;
            public readonly bool ModuleImported;

            private readonly HashSet<IPythonModule> _visited;

            public IDocumentAnalysis Analysis => Module.Analysis;
            public string FullName => string.Join('.', NameParts);

            public ModuleInfo(IDocumentAnalysis document) :
                this(document, module: null, new List<string>(), moduleImported: false) {
            }

            private ModuleInfo(IDocumentAnalysis document, IPythonModule module, List<string> nameParts, bool moduleImported) :
                this() {
                CurrentFileAnalysis = document;
                Module = module;
                NameParts = nameParts;
                ModuleImported = moduleImported;

                _visited = new HashSet<IPythonModule>();
            }

            public bool CheckCircularImports() => Module != null && _visited.Add(Module);
            public void ForgetModule() => _visited.Remove(Module);

            public void AddName(string memberName) => NameParts.Add(memberName);
            public void PopName() => NameParts.RemoveAt(NameParts.Count - 1);

            public ModuleInfo With(IPythonModule module) {
                return new ModuleInfo(CurrentFileAnalysis, module, NameParts, moduleImported: true);
            }

            public ModuleInfo Reset(IPythonModule module) {
                Debug.Assert(_visited.Count == 0);

                NameParts.Clear();
                NameParts.Add(module.Name);

                return new ModuleInfo(CurrentFileAnalysis, module, NameParts, moduleImported: false);
            }
        }

        [DebuggerDisplay("{Symbol?.Name} Module:{IsModule} ({ModuleImported} {MemberImported})")]
        private struct ImportInfo {
            // only one that shows up in "__all__" will be imported
            // containing module is imported
            public readonly bool ModuleImported;
            // containing symbol is imported
            public readonly bool MemberImported;

            public readonly bool IsModule;
            public readonly IPythonType Symbol;

            public ImportInfo(bool moduleImported, bool memberImported, IPythonType symbol) :
                this(moduleImported, memberImported, symbol.MemberType == PythonMemberType.Module) {
                Symbol = symbol;
            }

            public ImportInfo(bool moduleImported, bool memberImported, bool isModule) {
                ModuleImported = moduleImported;
                MemberImported = memberImported;
                IsModule = isModule;
                Symbol = null;
            }
        }
    }
}

