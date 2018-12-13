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
using Microsoft.Python.Analysis.Analyzer.Modules;
using Microsoft.Python.Analysis.Analyzer.Types;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Documents {
    public class Document : PythonModule, IDocument, IAnalyzable {
        private readonly DocumentBuffer _buffer = new DocumentBuffer();
        private readonly object _analysisLock = new object();
        private readonly object _parseLock = new object();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly DocumentCreationOptions _options;

        private string _documentation = string.Empty;
        private TaskCompletionSource<IDocumentAnalysis> _tcs = new TaskCompletionSource<IDocumentAnalysis>();
        private IReadOnlyList<DiagnosticsEntry> _diagnostics = Array.Empty<DiagnosticsEntry>();
        private CancellationTokenSource _parseCts;
        private Task<PythonAst> _parsingTask;
        private IDocumentAnalysis _analysis;
        private PythonAst _ast;

        protected Document() : base(string.Empty) { }

        protected Document(string moduleName, string content, string filePath, Uri uri, ModuleType moduleType, DocumentCreationOptions options, IServiceContainer services)
            : base(moduleName, filePath, uri, services) {
            ModuleType = moduleType;
            Content = content;

            _options = options;
            _buffer.Reset(0, content);

            IsOpen = (_options & DocumentCreationOptions.Open) == DocumentCreationOptions.Open;
            _options = _options | (IsOpen ? DocumentCreationOptions.Analyze : 0);

            if ((options & DocumentCreationOptions.Ast) == DocumentCreationOptions.Ast) {
                ParseAsync().DoNotWait();
            }
        }

        #region Construction
        /// <summary>
        /// Creates document from the supplied content.
        /// </summary>
        public static IDocument Create(string moduleName, ModuleType moduleType, string filePath, Uri uri, string content, DocumentCreationOptions options, IServiceContainer services) {
            filePath = filePath ?? uri?.LocalPath;
            uri = uri ?? MakeDocumentUri(filePath);
            moduleName = moduleName ?? ModulePath.FromFullPath(filePath, isPackage: IsPackageCheck).FullName;
            switch (moduleType) {
                case ModuleType.User:
                    return new Document(moduleName, content, filePath, uri, ModuleType.User, options, services);
                case ModuleType.Library:
                    return new Document(moduleName, content, filePath, uri, ModuleType.Library, options, services);
                case ModuleType.Stub:
                    return new StubPythonModule(moduleName, )
                case ModuleType.Scraped:
                    return new ScrapedPythonModule(moduleName, )
                case ModuleType.Builtins:
                    return new BuiltinsPythonModule(moduleName, );
            }
        }
        #endregion

        #region IPythonType
        public override string Documentation {
            get {
                _documentation = _documentation ?? Ast?.Documentation;
                if (_documentation == null) {
                    var m = GetMember("__doc__");
                    _documentation = (m as AstPythonStringLiteral)?.Value ?? string.Empty;
                    if (string.IsNullOrEmpty(_documentation)) {
                        m = GetMember($"_{Name}");
                        _documentation = (m as LazyPythonModule)?.Documentation;
                        if (string.IsNullOrEmpty(_documentation)) {
                            _documentation = TryGetDocFromModuleInitFile();
                        }
                    }
                }

                return _documentation;
            }
        }
        #endregion

        #region IDocument
        public event EventHandler<EventArgs> NewAst;
        public event EventHandler<EventArgs> NewAnalysis;

        /// <summary>
        /// Module content version (increments after every change).
        /// </summary>
        public int Version => _buffer.Version;

        /// <summary>
        /// Indicates that the document is open in the editor.
        /// </summary>
        public bool IsOpen { get; set; }

        /// <summary>
        /// Module type (user, library, stub).
        /// </summary>
        public ModuleType ModuleType { get; }

        /// <summary>
        /// Returns module content (code).
        /// </summary>
        public virtual string Content { get; }
        #endregion

        #region IDisposable
        public void Dispose() => Dispose(true);
        protected virtual void Dispose(bool disposing) => _cts.Cancel();
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

        #region Parsing
        /// <summary>
        /// Returns document parse tree.
        /// </summary>
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
        /// <summary>
        /// Returns document content as string.
        /// </summary>
        public string GetContent() => _buffer.Text;

        /// <summary>
        /// Provides collection of parsing errors, if any.
        /// </summary>
        public IEnumerable<DiagnosticsEntry> GetDiagnostics() => _diagnostics.ToArray();

        public void Update(IEnumerable<DocumentChangeSet> changes) {
            lock (_parseLock) {
                _buffer.Update(changes);
                ParseAsync().DoNotWait();
            }
        }

        private Task ParseAsync() {
            _parseCts?.Cancel();
            _parseCts = new CancellationTokenSource();
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, _parseCts.Token);
            _parsingTask = Task.Run(() => Parse(linkedCts.Token));
            return _parsingTask;
        }

        private PythonAst Parse(CancellationToken cancellationToken) {
            var sink = new CollectingErrorSink();
            int version;
            Parser parser;

            lock (_parseLock) {
                version = _buffer.Version;
                parser = Parser.CreateParser(new StringReader(_buffer.Text), Interpreter.LanguageVersion, new ParserOptions {
                    StubFile = FilePath != null && Path.GetExtension(FilePath).Equals(".pyi", Python.Core.IO.FileSystem.StringComparison),
                    ErrorSink = sink
                });
            }

            var ast = parser.ParseFile();
            var astUpdated = false;

            lock (_parseLock) {
                cancellationToken.ThrowIfCancellationRequested();
                if (version == _buffer.Version) {
                    _ast = ast;
                    _diagnostics = sink.Diagnostics;
                    astUpdated = true;
                    NewAst?.Invoke(this, EventArgs.Empty);
                }
            }

            if (astUpdated && (_options & DocumentCreationOptions.Analyze) == DocumentCreationOptions.Analyze) {
                var analyzer = Services.GetService<IPythonAnalyzer>();
                analyzer.AnalyzeDocumentDependencyChainAsync(this, cancellationToken).DoNotWait();
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

        private string TryGetDocFromModuleInitFile() {
            if (string.IsNullOrEmpty(FilePath) || !FileSystem.FileExists(FilePath)) {
                return string.Empty;
            }

            try {
                using (var sr = new StreamReader(FilePath)) {
                    string quote = null;
                    string line;
                    while (true) {
                        line = sr.ReadLine()?.Trim();
                        if (line == null) {
                            break;
                        }
                        if (line.Length == 0 || line.StartsWithOrdinal("#")) {
                            continue;
                        }
                        if (line.StartsWithOrdinal("\"\"\"") || line.StartsWithOrdinal("r\"\"\"")) {
                            quote = "\"\"\"";
                        } else if (line.StartsWithOrdinal("'''") || line.StartsWithOrdinal("r'''")) {
                            quote = "'''";
                        }
                        break;
                    }

                    if (quote != null) {
                        // Check if it is a single-liner
                        if (line.EndsWithOrdinal(quote) && line.IndexOf(quote) < line.LastIndexOf(quote)) {
                            return line.Substring(quote.Length, line.Length - 2 * quote.Length).Trim();
                        }
                        var sb = new StringBuilder();
                        while (true) {
                            line = sr.ReadLine();
                            if (line == null || line.EndsWithOrdinal(quote)) {
                                break;
                            }
                            sb.AppendLine(line);
                        }
                        return sb.ToString();
                    }
                }
            } catch (IOException) { } catch (UnauthorizedAccessException) { }
            return string.Empty;
        }

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
