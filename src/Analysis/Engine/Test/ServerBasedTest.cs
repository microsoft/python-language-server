// Python Tools for Visual Studio
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
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer;
using Microsoft.Python.LanguageServer.Implementation;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using TestUtilities;

namespace AnalysisTests {
    public class ServerBasedTest {
        private Server _server;

        protected async Task<Server> CreateServerAsync(InterpreterConfiguration configuration = null, Uri rootUri = null) {
            configuration = configuration ?? PythonVersions.LatestAvailable2X ?? PythonVersions.LatestAvailable3X;
            configuration.AssertInstalled();

            _server = await new Server().InitializeAsync(configuration, rootUri);
            _server.Analyzer.EnableDiagnostics = true;
            _server.Analyzer.Limits = GetLimits();

            return _server;
        }

        protected virtual AnalysisLimits GetLimits() => AnalysisLimits.GetDefaultLimits();

        protected Uri GetDocument(string file) {
            if (!Path.IsPathRooted(file)) {
                file = TestData.GetPath(file);
            }
            return new Uri(file);
        }

        protected static Task<Tuple<string, int>> ApplyChange(
            Server s,
            Uri document,
            params DocumentChange[] e
        ) {
            var initialVersion = Math.Max((s.GetEntry(document) as IDocument)?.GetDocumentVersion(s.GetPart(document)) ?? 0, 0);
            return ApplyChange(s, document, initialVersion, initialVersion + 1, e);
        }

        protected static async Task<Tuple<string, int>> ApplyChange(
            Server s,
            Uri document,
            int initialVersion,
            int finalVersion,
            params DocumentChange[] e
        ) {

            var parseComplete = EventTaskSources.Server.OnParseComplete.Create(s);

            await s.DidChangeTextDocument(new DidChangeTextDocumentParams {
                textDocument = new VersionedTextDocumentIdentifier {
                    uri = document,
                    version = finalVersion,
                },
                contentChanges = e.Select(c => new TextDocumentContentChangedEvent {
                    range = c.WholeBuffer ? null : (Range?)c.ReplacedSpan,
                    text = c.InsertedText
                }).ToArray()
            }, CancellationToken.None);

            await parseComplete;

            var newVersion = -1;
            var code = (s.GetEntry(document) as IDocument)?.ReadDocument(s.GetPart(document), out newVersion).ReadToEnd();
            return Tuple.Create(code, newVersion);
        }

        protected async Task<IModuleAnalysis> GetStubBasedAnalysis(
            Server server,
            string code,
            AnalysisLimits limits,
            IEnumerable<string> searchPaths,
            IEnumerable<string> stubPaths) {

            if (limits != null) {
                server.Analyzer.Limits = limits;
            }
            server.Analyzer.SetSearchPaths(searchPaths);
            server.Analyzer.SetTypeStubPaths(stubPaths);

            var uri = await server.OpenDefaultDocumentAndGetUriAsync(code);
            return await server.GetAnalysisAsync(uri);
        }

        protected static string GetTypeshedPath() {
            var asmPath = Assembly.GetExecutingAssembly().GetAssemblyPath();
            return Path.Combine(Path.GetDirectoryName(asmPath), "Typeshed");
        }
    }
}
