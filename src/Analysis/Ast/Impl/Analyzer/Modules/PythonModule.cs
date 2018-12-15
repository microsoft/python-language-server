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
    /// <summary>
    /// Primary base for all modules and user documents. Provides access
    /// to AST and the module analysis.
    /// </summary>
    internal class PythonModule : IDocument, IAnalyzable, IDisposable {
        private readonly DocumentBuffer _buffer = new DocumentBuffer();
        private readonly CancellationTokenSource _allProcessingCts = new CancellationTokenSource();
        private readonly DocumentCreationOptions _options;
        private IReadOnlyList<DiagnosticsEntry> _diagnostics = Array.Empty<DiagnosticsEntry>();
        private string _documentation = string.Empty;

        private readonly object _analysisLock = new object();
        private TaskCompletionSource<IDocumentAnalysis> _analysisTcs;
        private CancellationTokenSource _linkedAnalysisCts; // cancellation token combined with the 'dispose' cts
        private IDocumentAnalysis _analysis = DocumentAnalysis.Empty;

        private CancellationTokenSource _parseCts;
        private CancellationTokenSource _linkedParseCts; // combined with 'dispose' cts
        private Task _parsingTask;
        private PythonAst _ast;
        private bool _loaded;

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

            _options = options;
            IsOpen = (_options & DocumentCreationOptions.Open) == DocumentCreationOptions.Open;
            _options = _options | (IsOpen ? DocumentCreationOptions.Analyze : 0);

            InitializeContent(content);
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
        public virtual IPythonType GetMember(string name) => _analysis.TopLevelMembers.GetMember(name);
        public virtual IEnumerable<string> GetMemberNames() => _analysis.TopLevelMembers.GetMemberNames();
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

        /// <summary>
        /// Ensures that module content is loaded and analysis has started.
        /// Typically module content is loaded at the creation time, but delay
        /// loaded (lazy) modules may choose to defer content retrieval and
        /// analysis until later time, when module members are actually needed.
        /// </summary>
        public void Load() {
            if (!_loaded) {
                InitializeContent(null);
            }
        }

        protected virtual string LoadContent() => FileSystem.ReadAllText(FilePath);

        private void InitializeContent(string content) {
            content = content ?? LoadContent();
            _buffer.Reset(0, content);

            _loaded = true;

            if ((_options & DocumentCreationOptions.Analyze) == DocumentCreationOptions.Analyze) {
                _analysisTcs = new TaskCompletionSource<IDocumentAnalysis>();
            }
            if ((_options & DocumentCreationOptions.Ast) == DocumentCreationOptions.Ast) {
                Parse();
            }
        }
        #endregion

        #region ILocatedMember
        public virtual IEnumerable<LocationInfo> Locations { get; }
        #endregion

        #region IDisposable
        public void Dispose() => Dispose(true);

        protected virtual void Dispose(bool disposing) {
            _allProcessingCts.Cancel();
            _allProcessingCts.Dispose();
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
        public string Content => _buffer.Text;
        #endregion

        #region Parsing
        /// <summary>
        /// Returns document parse tree.
        /// </summary>
        public async Task<PythonAst> GetAstAsync(CancellationToken cancellationToken = default) {
            Task t = null;
            while (t != _parsingTask) {
                cancellationToken.ThrowIfCancellationRequested();
                t = _parsingTask;
                try {
                    await t;
                    break;
                } catch (OperationCanceledException) {
                    // Parsing as canceled, try next task.
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            return _ast;
        }

        /// <summary>
        /// Provides collection of parsing errors, if any.
        /// </summary>
        public IEnumerable<DiagnosticsEntry> GetDiagnostics() => _diagnostics.ToArray();

        public void Update(IEnumerable<DocumentChangeSet> changes) {
            lock (_analysisLock) {
                ExpectedAnalysisVersion++;

                _linkedAnalysisCts?.Cancel();
                _analysisTcs = new TaskCompletionSource<IDocumentAnalysis>();

                _parseCts?.Cancel();
                _parseCts = new CancellationTokenSource();

                _linkedParseCts?.Dispose();
                _linkedParseCts = CancellationTokenSource.CreateLinkedTokenSource(_allProcessingCts.Token, _parseCts.Token);

                _buffer.Update(changes);
                Parse();
            }
        }

        private void Parse() {
            _parseCts?.Cancel();
            _parseCts = new CancellationTokenSource();
            _linkedParseCts?.Dispose();
            _linkedParseCts = CancellationTokenSource.CreateLinkedTokenSource(_allProcessingCts.Token, _parseCts.Token);
            _parsingTask = Task.Run(() => Parse(_linkedParseCts.Token), _linkedParseCts.Token);
        }

        private void Parse(CancellationToken cancellationToken) {
            var sink = new CollectingErrorSink();
            int version;
            Parser parser;

            lock (_analysisLock) {
                version = _buffer.Version;
                parser = Parser.CreateParser(new StringReader(_buffer.Text), Interpreter.LanguageVersion, new ParserOptions {
                    StubFile = FilePath != null && Path.GetExtension(FilePath).Equals(".pyi", FileSystem.StringComparison),
                    ErrorSink = sink
                });
            }

            var ast = parser.ParseFile();

            lock (_analysisLock) {
                cancellationToken.ThrowIfCancellationRequested();
                if (version != _buffer.Version) {
                    throw new OperationCanceledException();
                }
                _ast = ast;
                _diagnostics = sink.Diagnostics;
                _parsingTask = null;
            }

            NewAst?.Invoke(this, EventArgs.Empty);

            if ((_options & DocumentCreationOptions.Analyze) == DocumentCreationOptions.Analyze) {
                _linkedAnalysisCts?.Dispose();
                _linkedAnalysisCts = CancellationTokenSource.CreateLinkedTokenSource(_allProcessingCts.Token, cancellationToken);

                var analyzer = Services.GetService<IPythonAnalyzer>();
                if (ModuleType == ModuleType.User || ModuleType == ModuleType.Library) {
                    analyzer.AnalyzeDocumentDependencyChainAsync(this, _linkedAnalysisCts.Token).DoNotWait();
                } else {
                    analyzer.AnalyzeDocumentAsync(this, _linkedAnalysisCts.Token).DoNotWait();
                }
            }
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
                // The notification comes from the analyzer when it needs to invalidate
                // current analysis since one of the dependencies changed. Upon text
                // buffer change the version may be incremented twice - once in Update()
                // and then here. This is normal.
                ExpectedAnalysisVersion++;
                _analysisTcs = _analysisTcs ?? new TaskCompletionSource<IDocumentAnalysis>();
            }
        }

        /// <summary>
        /// Performs analysis of the document. Returns document global scope
        /// with declared variables and inner scopes. Does not analyze chain
        /// of dependencies, it is intended for the single file analysis.
        /// </summary>
        public async Task<IDocumentAnalysis> AnalyzeAsync(CancellationToken cancellationToken) {
            // Store current expected version so we can see if it still 
            // the same at the time the analysis completes.
            var analysisVersion = ExpectedAnalysisVersion;

            // Make sure the file is parsed ans the AST is up to date.
            var ast = await GetAstAsync(cancellationToken);

            // Now run the analysis.
            var walker = new AnalysisWalker(Services, this, ast, suppressBuiltinLookup: ModuleType == ModuleType.Builtins);
            ast.Walk(walker);
            cancellationToken.ThrowIfCancellationRequested();

            // Note that we do not set the new analysis here and rather let
            // Python analyzer to call NotifyAnalysisComplete.
            var gs = walker.Complete();
            return new DocumentAnalysis(this, analysisVersion, gs);
        }

        public virtual bool NotifyAnalysisComplete(IDocumentAnalysis analysis) {
            lock (_analysisLock) {
                if (analysis.Version == ExpectedAnalysisVersion) {
                    _analysis = analysis;
                    // Derived classes can override OnAnalysisComplete if they want
                    // to perform additional actions on the completed analysis such
                    // as declare additional variables, etc.
                    OnAnalysisComplete(analysis.GlobalScope as GlobalScope);
                    _analysisTcs.TrySetResult(analysis);
                    _analysisTcs = null;

                    NewAnalysis?.Invoke(this, EventArgs.Empty);
                    return true;
                }
                Debug.Assert(ExpectedAnalysisVersion > analysis.Version);
                return false;
            }
        }

        protected virtual void OnAnalysisComplete(GlobalScope gs) { }
        #endregion

        #region Analysis
        public IDocumentAnalysis GetAnyAnalysis() => _analysis;

        public Task<IDocumentAnalysis> GetAnalysisAsync(CancellationToken cancellationToken = default) {
            lock (_analysisLock) {
                if ((_options & DocumentCreationOptions.Analyze) != DocumentCreationOptions.Analyze) {
                    return Task.FromResult(DocumentAnalysis.Empty);
                }
                return _analysis.Version == ExpectedAnalysisVersion ? Task.FromResult(_analysis) : _analysisTcs.Task;
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
    }
}
