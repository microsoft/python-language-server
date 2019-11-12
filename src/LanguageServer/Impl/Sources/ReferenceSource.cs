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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Documents;
using Microsoft.Python.LanguageServer.Protocol;

namespace Microsoft.Python.LanguageServer.Sources {
    internal enum ReferenceSearchOptions {
        All,
        ExcludeLibraries
    }

    internal sealed class ReferenceSource {
        private const int FindReferencesAnalysisTimeout = 10000;
        private readonly IServiceContainer _services;

        public ReferenceSource(IServiceContainer services) {
            _services = services;
        }

        public async Task<Reference[]> FindAllReferencesAsync(Uri uri, SourceLocation location, ReferenceSearchOptions options, CancellationToken cancellationToken = default) {
            if (uri != null) {
                var analysis = await Document.GetAnalysisAsync(uri, _services, FindReferencesAnalysisTimeout, cancellationToken);

                var definitionSource = new DefinitionSource(_services);
                var definition = definitionSource.FindDefinition(analysis, location, out var definingMember);
                if (definition == null) {
                    return Array.Empty<Reference>();
                }

                var rootDefinition = definingMember.GetRootDefinition();
                if (rootDefinition != null) {
                    var name = definingMember.GetName();
                    var moduleUri = rootDefinition.Definition?.DocumentUri;
                    if (moduleUri != null) {
                        var rdt = _services.GetService<IRunningDocumentTable>();
                        var declaringModule = rdt.GetDocument(moduleUri);

                        // If it is an implicitly declared variable, such as function or a class
                        // then the location is invalid and the module is null. Use current module.
                        if (!string.IsNullOrEmpty(name) && (declaringModule.ModuleType == ModuleType.User || options == ReferenceSearchOptions.All)) {
                            return await FindAllReferencesAsync(name, declaringModule, rootDefinition, location, definitionSource, cancellationToken);
                        }
                    }
                }
            }
            return Array.Empty<Reference>();
        }

        private async Task<Reference[]> FindAllReferencesAsync(string name, IPythonModule declaringModule, ILocatedMember rootDefinition, SourceLocation location, DefinitionSource definitionSource,
            CancellationToken cancellationToken) {
            var candidateFiles = ScanClosedFiles(name, cancellationToken);
            var reloadRootDefinition = false;

            if (candidateFiles.Count > 0) {
                reloadRootDefinition = await AnalyzeFiles(declaringModule.Interpreter.ModuleResolution, candidateFiles, cancellationToken);
            }

            if (reloadRootDefinition) {
                var analysis = await Document.GetAnalysisAsync(declaringModule.Uri, _services, FindReferencesAnalysisTimeout, cancellationToken);
                var definition = definitionSource.FindDefinition(analysis, location, out var definingMember);
                if (definition == null) {
                    return Array.Empty<Reference>();
                }

                rootDefinition = definingMember.GetRootDefinition();
            }

            return rootDefinition.References
                .Select(r => new Reference { uri = r.DocumentUri, range = r.Span })
                .ToArray();
        }

        private List<(Uri path, long fileSize)> ScanClosedFiles(string name, CancellationToken cancellationToken) {
            var fs = _services.GetService<IFileSystem>();
            var rdt = _services.GetService<IRunningDocumentTable>();
            var interpreter = _services.GetService<IPythonInterpreter>();

            var root = interpreter.ModuleResolution.Root;
            if (root == null) {
                return new List<(Uri, long)>();
            }

            var interpreterPaths = interpreter.ModuleResolution.InterpreterPaths;
            var files = new List<(Uri, long)>();

            foreach (var filePath in fs.GetFiles(root, "*.py", SearchOption.AllDirectories).Select(Path.GetFullPath)) {
                try {
                    cancellationToken.ThrowIfCancellationRequested();
                    // Exclude files that are inside interpreter paths such as when
                    // virtual environment is inside the user workspace folder.
                    var fileDirectory = Path.GetDirectoryName(filePath);
                    if (interpreterPaths.Any(p => fs.IsPathUnderRoot(p, fileDirectory))) {
                        continue;
                    }

                    var uri = new Uri(filePath);
                    var doc = rdt.GetDocument(uri);
                    if (doc != null) {
                        continue;
                    }

                    var content = fs.ReadTextWithRetry(filePath);
                    if (content == null) {
                        continue;
                    }

                    if (content.Contains(name)) {
                        files.Add((uri, fs.FileSize(filePath)));
                    }
                } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }

            return files;
        }

        private async Task<bool> AnalyzeFiles(IModuleManagement moduleManagement, IEnumerable<(Uri path, long fileSize)> files, CancellationToken cancellationToken) {
            var analysisTasks = new List<Task>();
            foreach (var (path, fileSize) in files) {
                if (moduleManagement.TryAddModulePath(path.ToAbsolutePath(), fileSize, false, out var fullName)) {
                    var module = moduleManagement.GetOrLoadModule(fullName);
                    if (module is IDocument document) {
                        analysisTasks.Add(document.GetAnalysisAsync(cancellationToken: cancellationToken));
                    }
                }
            }

            if (analysisTasks.Count > 0) {
                await Task.WhenAll(analysisTasks);
            }

            return analysisTasks.Count > 0;
        }
    }
}
