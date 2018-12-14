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
using Microsoft.Python.Analysis.Analyzer.Types;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Modules {
    internal class PythonModule : IDocument, IAnalyzable {
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
        private string _content;

        protected IDictionary<string, IPythonType> Members { get; set; } = new Dictionary<string, IPythonType>();
        protected ILogger Log { get; }
        protected IFileSystem FileSystem { get; }
        protected IServiceContainer Services { get; }

        protected PythonModule(string name, ModuleType moduleType, IServiceContainer services) {
            Check.ArgumentNotNull(nameof(name), name);
            Name = name;
            Services = services;
            ModuleType = moduleType;
        }

        internal PythonModule(string moduleName, string content, string filePath, Uri uri, ModuleType moduleType, DocumentCreationOptions options, IServiceContainer services)
            : this(moduleName, moduleType, services) {
            Check.ArgumentNotNull(nameof(services), services);

            FileSystem = services.GetService<IFileSystem>();
            Log = services.GetService<ILogger>();
            Interpreter = services.GetService<IPythonInterpreter>();
            Locations = new[] { new LocationInfo(filePath, uri, 1, 1) };

            if (uri == null && !string.IsNullOrEmpty(filePath)) {
                Uri.TryCreate(filePath, UriKind.Absolute, out uri);
            }
            Uri = uri;
            FilePath = filePath ?? uri?.LocalPath;

            _content = content;
            _options = options;

            IsOpen = (_options & DocumentCreationOptions.Open) == DocumentCreationOptions.Open;
            _options = _options | (IsOpen ? DocumentCreationOptions.Analyze : 0);

            if ((options & DocumentCreationOptions.Ast) == DocumentCreationOptions.Ast) {
                ParseAsync().DoNotWait();
            }
        }

        #region IPythonType
        public string Name { get; }
        public virtual IPythonModule DeclaringModule => null;
        public BuiltinTypeId TypeId => BuiltinTypeId.Module;
        public bool IsBuiltin => true;
        public bool IsTypeFactory => false;
        public IPythonFunction GetConstructor() => null;
        public PythonMemberType MemberType => PythonMemberType.Module;

        public virtual string Documentation {
            get {
                _documentation = _documentation ?? _ast?.Documentation;
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

        #region IMemberContainer
        public virtual IPythonType GetMember(string name) => Members.TryGetValue(name, out var m) ? m : null;
        public virtual IEnumerable<string> GetMemberNames() => Members.Keys.ToArray();
        #endregion

        #region IPythonFile
        public virtual string FilePath { get; }
        public virtual Uri Uri { get; }
        #endregion

        #region IPythonModule
        public IPythonInterpreter Interpreter { get; }

        [DebuggerStepThrough]
        public virtual IEnumerable<string> GetChildrenModuleNames() => GetChildModuleNames(FilePath, Name, Interpreter);

        private IEnumerable<string> GetChildModuleNames(string filePath, string prefix, IPythonInterpreter interpreter) {
            if (interpreter == null || string.IsNullOrEmpty(filePath)) {
                yield break;
            }
            var searchPath = Path.GetDirectoryName(filePath);
            if (!FileSystem.DirectoryExists(searchPath)) {
                yield break;
            }

            foreach (var n in ModulePath.GetModulesInPath(
                searchPath,
                recurse: false,
                includePackages: true
            ).Select(mp => mp.ModuleName).Where(n => !string.IsNullOrEmpty(n))) {
                yield return n;
            }
        }
        #endregion

        #region ILocatedMember
        public virtual IEnumerable<LocationInfo> Locations { get; }
        #endregion

        #region IDisposable
        public void Dispose() => Dispose(true);
        protected virtual void Dispose(bool disposing) => _cts.Cancel();
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
        public virtual string Content {
            get {
                if (_content == null) {
                    _content = LoadFile();
                    _buffer.Reset(0, _content);
                }
                return _content;
            }
        }

        protected virtual string LoadFile() => FileSystem.ReadAllText(FilePath);
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
                    StubFile = FilePath != null && Path.GetExtension(FilePath).Equals(".pyi", FileSystem.StringComparison),
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

        #region IAnalyzable
        /// <summary>
        /// Expected version of the analysis when asynchronous operations complete.
        /// Typically every change to the document or documents that depend on it
        /// increment the expected version. At the end of the analysis if the expected
        /// version is still the same, the analysis is applied to the document and
        /// becomes available to consumers.
        /// </summary>
        public int ExpectedAnalysisVersion { get; private set; }

        /// <summary>
        /// Notifies document that analysis is now pending. Typically document increments 
        /// the expected analysis version. The method can be called repeatedly without
        /// calling `CompleteAnalysis` first. The method is invoked for every dependency
        /// in the chain to ensure that objects know that their dependencies have been
        /// modified and the current analysis is no longer up to date.
        /// </summary>
        public void NotifyAnalysisPending() {
            lock (_analysisLock) {
                ExpectedAnalysisVersion++;
                if (_tcs == null || _tcs.Task.IsCanceled || _tcs.Task.IsCompleted || _tcs.Task.IsFaulted) {
                    _tcs = new TaskCompletionSource<IDocumentAnalysis>();
                }
            }
        }

        /// <summary>
        /// Performs analysis of the document. Returns document global scope
        /// with declared variables and inner scopes.
        /// </summary>
        public async Task<IGlobalScope> AnalyzeAsync(CancellationToken cancellationToken) {
            var analysisVersion = ExpectedAnalysisVersion;

            var ast = await GetAstAsync(cancellationToken);
            var walker = new AnalysisWalker(Services, this, ast, suppressBuiltinLookup: ModuleType == ModuleType.Builtins);
            ast.Walk(walker);
            return walker.Complete();
        }

        public virtual bool NotifyAnalysisComplete(IDocumentAnalysis analysis, int analysisVersion) {
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

        protected virtual void OnAnalysisComplete() { }
        #endregion

        #region Analysis
        public Task<IDocumentAnalysis> GetAnalysisAsync(CancellationToken cancellationToken = default) {
            lock (_analysisLock) {
                return _tcs?.Task;
            }
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
