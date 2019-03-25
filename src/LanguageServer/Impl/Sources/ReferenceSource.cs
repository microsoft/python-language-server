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
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Sources {
    internal sealed class ReferenceSource {
        private readonly IServiceContainer _services;
        private readonly string _rootPath;

        public ReferenceSource(IServiceContainer services, string rootPath) {
            _services = services;
            _rootPath = rootPath;
        }

        public async Task<Reference[]> FindAllReferences(IDocumentAnalysis analysis, ILocatedMember definingMember, SourceLocation location, CancellationToken cancellationToken) {
            if (definingMember.Parent == null) {
                // Basic, single-file case
                return definingMember.References
                    .Select(r => new Reference { uri = analysis.Document.Uri, range = r.Span })
                    .ToArray();
            }

            // Get to the root definition
            for (; definingMember.Parent != null; definingMember = definingMember.Parent) { }
            var module = definingMember.DeclaringModule;

            var closedFiles = ParseClosedFiles(cancellationToken);
            var candidateFiles = ScanFiles(closedFiles, Path.GetFileNameWithoutExtension(module.FilePath), cancellationToken);
            await AnalyzeFiles(candidateFiles, cancellationToken);

            return definingMember.References
                .Select(r => new Reference { uri = analysis.Document.Uri, range = r.Span })
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
            var nextCandidateNames = new HashSet<string>();
            var candidateFiles = new HashSet<string>();

            while (candidateNames.Count > 0) {
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

        private async Task AnalyzeAsync(string filePath, IRunningDocumentTable rdt, CancellationToken cancellationToken) {
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
                foreach (var n in node.Names.ExcludeDefault()) {
                    if (_names.Any(x => n.MakeString().Contains(x))) {
                        IsCandidate = true;
                        throw new OperationCanceledException();
                    }
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
