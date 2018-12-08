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
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Idle;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Documents {
    public sealed class Document : AstPythonModule, IDocument, IAnalyzable {
        private readonly object _analysisLock = new object();
        private readonly IFileSystem _fs;
        private readonly DocumentBuffer _buffer = new DocumentBuffer();
        private readonly object _lock = new object();

        private TaskCompletionSource<IDocumentAnalysis> _tcs = new TaskCompletionSource<IDocumentAnalysis>();
        private IReadOnlyList<DiagnosticsEntry> _diagnostics = Array.Empty<DiagnosticsEntry>();
        private CancellationTokenSource _cts;
        private Task<PythonAst> _parsingTask;
        private IDocumentAnalysis _analysis;
        private PythonAst _ast;

        /// <summary>
        /// Creates document from a disk file. Module name is determined automatically from the file name.
        /// </summary>
        public static IDocument FromFile(IPythonInterpreter interpreter, string filePath)
            => FromFile(interpreter, filePath, null);

        /// <summary>
        /// Creates document from a disk file.
        /// </summary>
        public static IDocument FromFile(IPythonInterpreter interpreter, string filePath, string moduleName) {
            var fs = interpreter.Services.GetService<IFileSystem>();
            return FromContent(interpreter, fs.ReadAllText(filePath), null, filePath, moduleName);
        }

        /// <summary>
        /// Creates document from a supplied content.
        /// </summary>
        public static IDocument FromContent(IPythonInterpreter interpreter, string content, Uri uri, string filePath, string moduleName) {
            filePath = filePath ?? uri?.LocalPath;
            uri = uri ?? MakeDocumentUri(filePath);
            moduleName = moduleName ?? ModulePath.FromFullPath(filePath, isPackage: IsPackageCheck).FullName;
            return new Document(interpreter, content, uri, filePath, moduleName);
        }

        private Document(IPythonInterpreter interpreter, string content, Uri uri, string filePath, string moduleName):
            base(moduleName, filePath, uri, interpreter) {

            _fs = Interpreter.Services.GetService<IFileSystem>();

            _buffer.Reset(0, content);
            ParseAsync().DoNotWait();
        }

        public event EventHandler<EventArgs> NewAst;
        public event EventHandler<EventArgs> NewAnalysis;

        public int Version => _buffer.Version;
        public bool IsOpen { get; set; }

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
                }
            }
            return _ast;
        }
        public string GetContent() => _buffer.Text;
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
                parser = Parser.CreateParser(new StringReader(_buffer.Text), Interpreter.LanguageVersion, new ParserOptions {
                    StubFile = FilePath != null && Path.GetExtension(FilePath).Equals(".pyi", _fs.StringComparison),
                    ErrorSink = sink
                });
            }

            var ast = parser.ParseFile();

            lock (_lock) {
                cancellationToken.ThrowIfCancellationRequested();
                if (version == _buffer.Version) {
                    _ast = ast;
                    _diagnostics = sink.Diagnostics;
                    NewAst?.Invoke(this, EventArgs.Empty);
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
                    _analysis = analysis;
                    _tcs.TrySetResult(analysis);
                    NewAnalysis?.Invoke(this, EventArgs.Empty);
                    return true;
                }
                Debug.Assert(ExpectedAnalysisVersion > analysisVersion);
                return false;
            }
        }
        #endregion

        #region Analysis

        public Task<IDocumentAnalysis> GetAnalysisAsync(CancellationToken cancellationToken = default) {
            lock (_analysisLock) {
                return _tcs?.Task;
            }
        }
        #endregion

        protected override PythonAst GetAst() => _ast;

        private static Uri MakeDocumentUri(string filePath) {
            if (string.IsNullOrEmpty(filePath)) {
                return null;
            }
            return !Path.IsPathRooted(filePath)
                ? new Uri("file:///LOCAL-PATH/{0}".FormatInvariant(filePath.Replace('\\', '/')))
                : new Uri(filePath);
        }

        private static bool IsPackageCheck(string path)
            => ModulePath.IsImportable(PathUtils.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
    }
}
