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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Core.Idle;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Shell;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Documents {
    /// </inheritdoc>
    public sealed class Document : IDocument, IAnalyzable {
        private readonly IFileSystem _fs;
        private readonly IIdleTimeService _idleTimeService;
        private readonly IPythonAnalyzer _analyzer;
        private readonly DocumentBuffer _buffer = new DocumentBuffer();
        private readonly object _lock = new object();

        private IReadOnlyList<DiagnosticsEntry> _diagnostics;
        private CancellationTokenSource _cts;
        private Task<PythonAst> _parsingTask;
        private PythonAst _ast;

        public Document(string name, string filePath, Uri uri, bool isInWorkspace, string content, IServiceContainer services) {
            Check.ArgumentNotNull(nameof(name), name);
            Check.ArgumentNotNull(nameof(filePath), filePath);
            Check.ArgumentNotNull(nameof(services), services);

            if (uri == null && !Uri.TryCreate(filePath, UriKind.Absolute, out uri)) {
                throw new ArgumentException("Unable to create URI from the file path");
            }
            Name = name;
            FilePath = filePath;
            Uri = uri;
            IsInWorkspace = isInWorkspace;

            _fs = services.GetService<IFileSystem>();
            _analyzer = services.GetService<IPythonAnalyzer>();
            _idleTimeService = services.GetService<IIdleTimeService>();

            content = content ?? _fs.ReadAllText(FilePath);
            _buffer.Reset(0, content);

            ParseAsync().DoNotWait();
        }

        public Document(Uri uri, string content, bool isInWorkspace, IServiceContainer services) :
            this(Path.GetFileNameWithoutExtension(uri.LocalPath), uri.LocalPath, uri, isInWorkspace, content, services) { }

        public event EventHandler<EventArgs> NewAst;
        public event EventHandler<EventArgs> NewAnalysis;

        public string FilePath { get; }
        public string Name { get; }
        public Uri Uri { get; }

        public int Version { get; private set; }
        public bool IsInWorkspace { get; }
        public bool IsOpen { get; set; }

        public IPythonModule PythonModule => throw new NotImplementedException();

        public void Dispose() { }

        #region Parsing
        public async Task<PythonAst> GetAstAsync(CancellationToken cancellationToken = default) {
            Task t = null;
            while (t != _parsingTask && !cancellationToken.IsCancellationRequested) {
                t = _parsingTask;
                try {
                    await t;
                    break;
                } catch (OperationCanceledException) {
                    // Parsing as canceled, try next task.
                    continue;
                }
            }
            return _ast;
        }

        public string GetContent() => _buffer.Text;
        public TextReader GetReader() => new StringReader(_buffer.Text);
        public Stream GetStream() => new MemoryStream(Encoding.UTF8.GetBytes(_buffer.Text.ToCharArray()));
        public IEnumerable<DiagnosticsEntry> GetDiagnostics() => _diagnostics.ToArray();

        public void Update(IEnumerable<DocumentChangeSet> changes) {
            lock (_lock) {
                _buffer.Update(changes);
            }
            ParseAsync().DoNotWait();
        }

        private Task ParseAsync() {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            _parsingTask = Task.Run(() => Parse(_cts.Token));
            return _parsingTask;
        }

        private PythonAst Parse(CancellationToken cancellationToken) {
            var sink = new CollectingErrorSink();
            int version;
            Parser parser;

            lock (_lock) {
                version = _buffer.Version;
                parser = Parser.CreateParser(GetReader(), _analyzer.Interpreter.LanguageVersion, new ParserOptions {
                    StubFile = Path.GetExtension(FilePath).Equals(".pyi", _fs.StringComparison),
                    ErrorSink = sink
                });
            }

            var ast = parser.ParseFile();

            lock (_lock) {
                cancellationToken.ThrowIfCancellationRequested();
                if (version == _buffer.Version) {
                    _ast = ast;
                    _diagnostics = sink.Diagnostics;
                }
            }
            return ast;
        }

        private class CollectingErrorSink : ErrorSink {
            private readonly List<DiagnosticsEntry> _diagnostics = new List<DiagnosticsEntry>();

            public IReadOnlyList<DiagnosticsEntry> Diagnostics => _diagnostics;
            public override void Add(string message, SourceSpan span, int errorCode, Severity severity)
                => _diagnostics.Add(new DiagnosticsEntry(message, span, errorCode, severity));
        }
        #endregion

        private TaskCompletionSource<IDocumentAnalysis> _tcs;
        private readonly object _analysisLock = new object();

        #region IAnalyzable
        public int ExpectedAnalysisVersion { get; private set; }

        public void NotifyAnalysisPending() {
            lock (_analysisLock) {
                ExpectedAnalysisVersion++;
                if (_tcs == null || _tcs.Task.IsCanceled || _tcs.Task.IsCompleted || _tcs.Task.IsFaulted) {
                    _tcs = new TaskCompletionSource<IDocumentAnalysis>();
                }
            }
        }
        public bool NotifyAnalysisComplete(IDocumentAnalysis analysis, int analysisVersion) {
            lock (_analysisLock) {
                if (analysisVersion == ExpectedAnalysisVersion) {
                    _tcs.TrySetResult(analysis);
                    return true;
                }
                Debug.Assert(ExpectedAnalysisVersion > analysisVersion);
                return false;
            }
        }
        #endregion

        #region Analysis
        public Task<IDocumentAnalysis> GetAnalysisAsync(CancellationToken cancellationToken = default) => _tcs?.Task;
        #endregion

        public static Uri MakeDocumentUri(string filePath) {
            Uri u;
            if (!Path.IsPathRooted(filePath)) {
                u = new Uri("file:///LOCAL-PATH/{0}".FormatInvariant(filePath.Replace('\\', '/')));
            } else {
                u = new Uri(filePath);
            }
            return u;
        }
    }
}
