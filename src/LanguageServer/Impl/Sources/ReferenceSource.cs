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
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Sources {
    internal enum ReferenceSearchOptions {
        All,
        ExcludeLibraries
    }

    internal sealed class ReferenceSource {
        private const int FindReferencesAnalysisTimeout = 10000;
        private readonly IServiceContainer _services;
        private readonly string _rootPath;

        public ReferenceSource(IServiceContainer services, string rootPath) {
            _services = services;
            _rootPath = rootPath;
        }

        public async Task<Reference[]> FindAllReferencesAsync(Uri uri, SourceLocation location, ReferenceSearchOptions options, CancellationToken cancellationToken = default) {
            var analysis = await Document.GetAnalysisAsync(uri, _services, FindReferencesAnalysisTimeout, cancellationToken);
            var definition = new DefinitionSource(_services).FindDefinition(analysis, location, out var definingMember);
            if (definition != null) {
                var rootDefinition = definingMember.GetRootDefinition();
                if (rootDefinition.DeclaringModule.ModuleType == ModuleType.User || options == ReferenceSearchOptions.All) {
                    return await FindAllReferencesAsync(rootDefinition, cancellationToken);
                }
            }
            return Array.Empty<Reference>();
        }

        private async Task<Reference[]> FindAllReferencesAsync(ILocatedMember rootDefinition, CancellationToken cancellationToken) {
            var module = rootDefinition.DeclaringModule;

            var closedFiles = ParseClosedFiles(cancellationToken);
            var candidateFiles = ScanFiles(closedFiles, Path.GetFileNameWithoutExtension(module.FilePath), cancellationToken);
            await AnalyzeFiles(candidateFiles, cancellationToken);

            return rootDefinition.References
                .Select(r => new Reference { uri = new Uri(r.FilePath), range = r.Span })
                .ToArray();
        }

        private Dictionary<string, PythonAst> ParseClosedFiles(CancellationToken cancellationToken) {
            var fs = _services.GetService<IFileSystem>();
            var rdt = _services.GetService<IRunningDocumentTable>();
            var interpreter = _services.GetService<IPythonInterpreter>();
            var closedFiles = new Dictionary<string, PythonAst>();

            try {
                foreach (var filePath in fs.GetFiles(_rootPath, "*.py", SearchOption.AllDirectories)) {
                    cancellationToken.ThrowIfCancellationRequested();
                    var doc = rdt.GetDocument(new Uri(filePath));
                    if (doc != null) {
                        continue;
                    }
                    var content = fs.ReadTextWithRetry(filePath);
                    using (var s = new StringReader(content)) {
                        var parser = Parser.CreateParser(s, interpreter.LanguageVersion);
                        var ast = parser.ParseFile();
                        closedFiles[filePath] = ast;
                    }
                }
            } catch (IOException) { } catch (UnauthorizedAccessException) { }

            return closedFiles;
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

        private async Task AnalyzeFiles(IEnumerable<string> files, CancellationToken cancellationToken) {
            var rdt = _services.GetService<IRunningDocumentTable>();
            foreach (var f in files) {
                await AnalyzeAsync(f, rdt, cancellationToken);
            }
        }

        private static async Task AnalyzeAsync(string filePath, IRunningDocumentTable rdt, CancellationToken cancellationToken) {
            if (rdt.GetDocument(new Uri(filePath)) != null) {
                return; // Already opened by another analysis.
            }

            var mco = new ModuleCreationOptions {
                ModuleName = Path.GetFileNameWithoutExtension(filePath),
                FilePath = filePath,
                Uri = new Uri(filePath),
                ModuleType = ModuleType.User
            };
            var document = rdt.AddModule(mco);
            await document.GetAnalysisAsync(Timeout.Infinite, cancellationToken);
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
