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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Analyzer.Expressions;
using Microsoft.Python.Analysis.Caching;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Generators;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Core.Threading;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.LanguageServer.Sources;
using Microsoft.Python.Parsing.Ast;
using Range = Microsoft.Python.Core.Text.Range;

namespace Microsoft.Python.LanguageServer.CodeActions {
    internal sealed class StubGenerationRefactoringCodeActionProvider : IRefactoringCodeActionProvider, ICommandHandler {
        public static readonly StubGenerationRefactoringCodeActionProvider Instance = new StubGenerationRefactoringCodeActionProvider();

        public ImmutableArray<string> SupportingCommands => ImmutableArray<string>.Create(WellKnownCommands.StubGeneration);

        public Task<IEnumerable<CodeAction>> GetCodeActionsAsync(IDocumentAnalysis analysis, CodeActionSettings settings, Range range, CancellationToken cancellationToken) {
            if (!settings.GetRefactoringOption("generation.stub", false)) {
                // disabled
                return SpecializedTasks.EmptyEnumerable<CodeAction>();
            }

            var fileSystem = analysis.ExpressionEvaluator.Services.GetService<IFileSystem>();
            if (fileSystem == null) {
                return SpecializedTasks.EmptyEnumerable<CodeAction>();
            }

            var stubBasePath = GetStubBasePath(analysis.ExpressionEvaluator.Services, settings);
            if (stubBasePath == null) {
                return SpecializedTasks.EmptyEnumerable<CodeAction>();
            }

            var indexSpan = range.ToIndexSpan(analysis.Ast);
            var finder = new ExpressionFinder(analysis.Ast, new FindExpressionOptions() { Names = true, ImportAsNames = true, ImportNames = true });
            finder.Get(indexSpan.Start, indexSpan.End, out var node, out var statement, out var scope);
            if (!(node is NameExpression)) {
                return SpecializedTasks.EmptyEnumerable<CodeAction>();
            }

            if (!(statement is ImportStatement || statement is FromImportStatement)) {
                return SpecializedTasks.EmptyEnumerable<CodeAction>();
            }

            var module = GetModule(analysis, scope, node, cancellationToken);
            if (module == null || module.Analysis is EmptyAnalysis || module.FilePath == null) {
                return SpecializedTasks.EmptyEnumerable<CodeAction>();
            }

            if (module.Stub != null) {
                // for now, we bail out but later we would like to check whether we can improve existing stub
                // with information we have
                return SpecializedTasks.EmptyEnumerable<CodeAction>();
            }

            var modulePath = analysis.Document.Interpreter.ModuleResolution.FindModule(module.FilePath);
            if (string.IsNullOrEmpty(modulePath.FullName)) {
                return SpecializedTasks.EmptyEnumerable<CodeAction>();
            }

            var stubPath = GetStubPath(stubBasePath, modulePath);
            if (fileSystem.FileExists(stubPath)) {
                // stub already exist
                return SpecializedTasks.EmptyEnumerable<CodeAction>();
            }

            var title = Resources.GenerateStub_0_1.FormatUI(module.Name, stubPath);
            var codeAction = new CodeAction() {
                title = title,
                kind = CodeActionKind.Refactor,
                command = new Command() {
                    title = title,
                    command = WellKnownCommands.StubGeneration,
                    arguments = new object[] { analysis.Document.Uri, module.Name, module.FilePath, stubPath }
                }
            };

            return Task.FromResult<IEnumerable<CodeAction>>(new CodeAction[] { codeAction });
        }

        public Task<bool> HandleAsync(IServiceContainer services, string command, object[] arguments, CancellationToken cancellationToken) {
            if (arguments?.Length != 4 || command != WellKnownCommands.StubGeneration) {
                return SpecializedTasks.False;
            }

            var interpreter = services.GetService<IPythonInterpreter>();
            var module = interpreter?.ModuleResolution.GetOrLoadModule((string)arguments[1]);
            if (module?.FilePath != (string)arguments[2]) {
                return SpecializedTasks.False;
            }

            var fileSystem = services.GetService<IFileSystem>();
            if (fileSystem == null) {
                return SpecializedTasks.False;
            }

            // for now, we trust path given in the argument
            var stubPath = (string)arguments[3];
            if (fileSystem.FileExists(stubPath)) {
                return SpecializedTasks.False;
            }

            var code = GenerateStub(services, stubPath, module, cancellationToken);

            try {
                // ensure directory exist
                var path = Path.GetDirectoryName(stubPath);
                fileSystem.CreateDirectory(path);

                fileSystem.WriteAllText(stubPath, code);
            } catch (Exception ex) {
                var logger = services.GetService<ILogger>();
                logger?.Log(TraceEventType.Error, $"writing stub file failed: {ex.ToString()}");
            }

            return SpecializedTasks.True;
        }

        private string GenerateStub(IServiceContainer services, string stubPath, IPythonModule module, CancellationToken cancellationToken) {
            if (HasTypeAnnotation(module)) {
                return GenerateFromModule(services, module, cancellationToken);
            } else {
                // we need to create stub from scratch
                return GenerateFromScrape(services, module, cancellationToken);
            }

            static bool HasTypeAnnotation(IPythonModule module) {
                // if module already has type annotation, then create stub from the module itself
                return false;
            }
        }

        private string GenerateFromModule(IServiceContainer services, IPythonModule module, CancellationToken cancellationToken) {
            // it looks like module already has type annotation. create stub from it
            var ast = module.Analysis.Ast;

            return string.Empty;
        }

        private static string GenerateFromScrape(IServiceContainer services,
            IPythonModule module,
            CancellationToken cancellationToken) {

            var interpreter = services.GetService<IPythonInterpreter>();
            var logger = services.GetService<ILogger>();
            return StubGenerator.Generate(interpreter, logger, module, GetScrapeArguments(interpreter, module.FilePath), cancellationToken);

            static string[] GetScrapeArguments(IPythonInterpreter interpreter, string filePath) {
                var mp = interpreter.ModuleResolution.FindModule(filePath);
                if (string.IsNullOrEmpty(mp.FullName)) {
                    return null;
                }

                var args = new List<string>();
                args.Add("-u8");
                args.Add(mp.ModuleName);
                args.Add(mp.LibraryPath);

                return args.ToArray();
            }
        }

        private string GetStubPath(string stubBasePath, ModulePath modulePath) {
            if (modulePath.Name == "__init__") {
                return Path.Combine(stubBasePath, modulePath.ModuleName.Replace('.', Path.DirectorySeparatorChar), $"{modulePath.Name}.pyi");
            }

            var endIndex = modulePath.ModuleName.LastIndexOf($".{modulePath.Name}");
            if (endIndex >= 0) {
                return Path.Combine(stubBasePath, modulePath.ModuleName.Substring(0, endIndex).Replace('.', Path.DirectorySeparatorChar), $"{modulePath.Name}.pyi");
            }

            return Path.Combine(stubBasePath, modulePath.ModuleName.Replace('.', Path.DirectorySeparatorChar), $"{modulePath.Name}.pyi");
        }

        private static string GetStubBasePath(IServiceContainer service, CodeActionSettings settings) {
            var baseStubPath = settings.GetRefactoringOption<string>("generation.stub.path", null);
            if (baseStubPath != null) {
                return baseStubPath;
            }

            var cacheService = service.GetService<ICacheFolderService>();
            if (cacheService != null) {
                return Path.Combine(cacheService.CacheFolder, "StubGenerated");
            }

            return null;
        }

        private static IPythonModule GetModule(IDocumentAnalysis analysis, ScopeStatement scope, Node node, CancellationToken cancellationToken) {
            var ancestors = analysis.Ast.GetAncestorsOrThis(node, cancellationToken);
            var moduleName = ancestors.LastOrDefault(n => n is ModuleName) as ModuleName;
            if (moduleName != null) {
                // for import Name or from Name import xxxx
                return GetModule(analysis, scope, moduleName);
            }

            // for .. import xxx as Name or from xxx import Name1 as Name2
            return GetModule(analysis, scope, ((NameExpression)node).Name);
        }

        private static IPythonModule GetModule(IDocumentAnalysis analysis, ScopeStatement scope, string name) {
            var eval = analysis.ExpressionEvaluator;
            using (eval.OpenScope(analysis.Document, scope)) {
                return eval.LookupNameInScopes(name, Analysis.Analyzer.LookupOptions.All) as IPythonModule;
            }
        }

        private static IPythonModule GetModule(IDocumentAnalysis analysis, ScopeStatement scope, ModuleName moduleName) {
            var module = GetModule(analysis, scope, moduleName.Names[0].Name);
            for (var i = 1; i < moduleName.Names.Count; i++) {
                module = module.GetMember<IPythonModule>(moduleName.Names[i].Name);
            }

            return module;
        }
    }
}
