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
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Documents;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.Parsing.Ast;

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

                var definition = new DefinitionSource(_services).FindDefinition(analysis, location, out var definingMember);
                if (definition == null) {
                    return Array.Empty<Reference>();
                }

                var rootDefinition = GetRootDefinition(definingMember);
                var name = definingMember.GetName();

                // If it is an implicitly declared variable, such as function
                // or a class. Use current module then.
                var declaringModule = rootDefinition.DeclaringModule ?? analysis.Document;
                if (!string.IsNullOrEmpty(name) && (declaringModule.ModuleType == ModuleType.User || options == ReferenceSearchOptions.All)) {
                    return await FindAllReferencesAsync(name, rootDefinition, cancellationToken);
                }
            }
            return Array.Empty<Reference>();
        }

        private async Task<Reference[]> FindAllReferencesAsync(string name, ILocatedMember rootDefinition, CancellationToken cancellationToken) {
            var candidateFiles = ScanClosedFiles(name, cancellationToken);
            await AnalyzeFiles(candidateFiles, cancellationToken);

            return rootDefinition.References
                .Select(r => new Reference { uri = new Uri(r.FilePath), range = r.Span })
                .ToArray();
        }

        private IEnumerable<Uri> ScanClosedFiles(string name, CancellationToken cancellationToken) {
            var fs = _services.GetService<IFileSystem>();
            var rdt = _services.GetService<IRunningDocumentTable>();
            var interpreter = _services.GetService<IPythonInterpreter>();

            var root = interpreter.ModuleResolution.Root;
            var interpreterPaths = interpreter.ModuleResolution.InterpreterPaths.ToArray();
            var files = new List<Uri>();

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
                    if (content.Contains(name)) {
                        files.Add(uri);
                    }
                } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }

            return files;
        }

        private IEnumerable<string> ScanFiles(IDictionary<string, PythonAst> closedFiles, string name, CancellationToken cancellationToken) {
            var candidateNames = new HashSet<string> { name };
            var candidateFiles = new HashSet<string>();

            while (candidateNames.Count > 0) {
                var nextCandidateNames = new HashSet<string>();

                foreach (var kvp in closedFiles.ToArray()) {
                    cancellationToken.ThrowIfCancellationRequested();

                    var w = new ImportsWalker(candidateNames);
                    try {
                        kvp.Value.Walk(w);
                    } catch (OperationCanceledException) { }

                    if (w.IsCandidate) {
                        candidateFiles.Add(kvp.Key);
                        nextCandidateNames.Add(Path.GetFileNameWithoutExtension(kvp.Key));
                        closedFiles.Remove(kvp.Key);
                    }
                }
                candidateNames = nextCandidateNames;
            }
            return candidateFiles;
        }

        private async Task AnalyzeFiles(IEnumerable<Uri> files, CancellationToken cancellationToken) {
            var rdt = _services.GetService<IRunningDocumentTable>();
            var analysisTasks = new List<Task>();
            foreach (var f in files) {
                analysisTasks.Add(GetOrOpenModule(f, rdt).GetAnalysisAsync(cancellationToken: cancellationToken));
            }

            await Task.WhenAll(analysisTasks);
        }

        private static IDocument GetOrOpenModule(Uri uri, IRunningDocumentTable rdt) {
            var document = rdt.GetDocument(uri);
            if (document != null) {
                return document; // Already opened by another analysis.
            }

            var filePath = uri.ToAbsolutePath();
            var mco = new ModuleCreationOptions {
                ModuleName = Path.GetFileNameWithoutExtension(filePath),
                FilePath = filePath,
                Uri = uri,
                ModuleType = ModuleType.User
            };

            return rdt.AddModule(mco);
        }

        private ILocatedMember GetRootDefinition(ILocatedMember lm) {
            for (; lm.Parent != null; lm = lm.Parent) { }
            return lm;
        }

        private class ImportsWalker : PythonWalker {
            private readonly HashSet<string> _names;

            public bool IsCandidate { get; private set; }

            public ImportsWalker(HashSet<string> names) {
                _names = names;
            }

            public override bool Walk(ImportStatement node) {
                if (node.Names.ExcludeDefault().Any(n => _names.Any(x => n.MakeString().Contains(x)))) {
                    IsCandidate = true;
                    throw new OperationCanceledException();
                }
                return false;
            }

            public override bool Walk(FromImportStatement node) {
                if (_names.Any(x => node.Root.MakeString().Contains(x))) {
                    IsCandidate = true;
                    throw new OperationCanceledException();
                }
                return false;
            }
        }
    }
}
