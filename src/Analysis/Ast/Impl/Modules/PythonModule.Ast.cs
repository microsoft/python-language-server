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
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Modules {
    internal partial class PythonModule {
        #region Parsing
        /// <summary>
        /// Returns document parse tree.
        /// </summary>
        public async Task<PythonAst> GetAstAsync(CancellationToken cancellationToken = default) {
            Task t = null;
            while (true) {
                lock (AnalysisLock) {
                    if (t == _parsingTask) {
                        break;
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    t = _parsingTask;
                }
                try {
                    await (t ?? Task.CompletedTask);
                    break;
                } catch (OperationCanceledException) {
                    // Parsing as canceled, try next task.
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            return this.GetAst();
        }

        public PythonAst GetAnyAst() => GetAstNode<PythonAst>(this);

        /// <summary>
        /// Provides collection of parsing errors, if any.
        /// </summary>
        public IEnumerable<DiagnosticsEntry> GetParseErrors() => _parseErrors.ToArray();

        private void Parse() {
            _parseCts?.Cancel();
            _parseCts = new CancellationTokenSource();

            _linkedParseCts?.Dispose();
            _linkedParseCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeToken.CancellationToken, _parseCts.Token);

            ContentState = State.Parsing;
            _parsingTask = Task.Run(() => Parse(_linkedParseCts.Token), _linkedParseCts.Token);
        }

        private void Parse(CancellationToken cancellationToken) {
            CollectingErrorSink sink = null;
            int version;
            Parser parser;

            //Log?.Log(TraceEventType.Verbose, $"Parse begins: {Name}");

            lock (AnalysisLock) {
                version = _buffer.Version;
                var options = new ParserOptions {
                    StubFile = FilePath != null && Path.GetExtension(FilePath).Equals(".pyi", FileSystem.StringComparison)
                };
                if (ModuleType == ModuleType.User) {
                    sink = new CollectingErrorSink();
                    options.ErrorSink = sink;
                }
                parser = Parser.CreateParser(new StringReader(_buffer.Text), Interpreter.LanguageVersion, options);
            }

            var ast = parser.ParseFile(Uri);

            //Log?.Log(TraceEventType.Verbose, $"Parse complete: {Name}");

            lock (AnalysisLock) {
                cancellationToken.ThrowIfCancellationRequested();
                if (version != _buffer.Version) {
                    throw new OperationCanceledException();
                }

                // Stored nodes are no longer valid.
                _astMap.Clear();
                _astMap[this] = ast;

                _parseErrors = sink?.Diagnostics ?? Array.Empty<DiagnosticsEntry>();

                // Do not report issues with libraries or stubs
                if (sink != null) {
                    _diagnosticsService?.Replace(Uri, _parseErrors, DiagnosticSource.Parser);
                }

                ContentState = State.Parsed;
                Analysis = new EmptyAnalysis(Services, this);
            }

            NewAst?.Invoke(this, EventArgs.Empty);

            if (ContentState < State.Analyzing) {
                ContentState = State.Analyzing;

                var analyzer = Services.GetService<IPythonAnalyzer>();
                analyzer.EnqueueDocumentForAnalysis(this, version);
            }

            lock (AnalysisLock) {
                _parsingTask = null;
            }
        }

        private class CollectingErrorSink : ErrorSink {
            private readonly List<DiagnosticsEntry> _diagnostics = new List<DiagnosticsEntry>();

            public IReadOnlyList<DiagnosticsEntry> Diagnostics => _diagnostics;
            public override void Add(string message, SourceSpan span, int errorCode, Severity severity)
                => _diagnostics.Add(new DiagnosticsEntry(message, span, $"parser-{errorCode}", severity, DiagnosticSource.Parser));
        }
        #endregion

        #region IAstNodeContainer
        public T GetAstNode<T>(object o) where T : Node {
            lock (AnalysisLock) {
                return _astMap.TryGetValue(o, out var n) ? (T)n : null;
            }
        }

        public void AddAstNode(object o, Node n) {
            lock (AnalysisLock) {
                Debug.Assert(!_astMap.ContainsKey(o) || _astMap[o] == n);
                _astMap[o] = n;
            }
        }

        public void ClearAst() {
            lock (AnalysisLock) {
                if (ModuleType != ModuleType.User) {
                    _astMap.Clear();
                }
            }
        }
        public void ClearContent() {
            lock (AnalysisLock) {
                if (ModuleType != ModuleType.User) {
                    _buffer.Reset(_buffer.Version, string.Empty);
                }
            }
        }
        #endregion

        private PythonAst RecreateAst() {
            lock (AnalysisLock) {
                ContentState = State.None;
                LoadContent();
                var parser = Parser.CreateParser(new StringReader(_buffer.Text), Interpreter.LanguageVersion, ParserOptions.Default);
                var ast = parser.ParseFile(Uri);
                ContentState = State.Parsed;
                return ast;
            }
        }
    }
}
