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
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Core.Disposables;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Modules {
    /// <summary>
    /// Primary base for all modules and user documents. Provides access
    /// to AST and the module analysis.
    /// </summary>
    [DebuggerDisplay("{Name} : {ModuleType}")]
    public class PythonModule : IDocument, IAnalyzable, IEquatable<IPythonModule> {
        private enum State {
            None,
            Loading,
            Loaded,
            Parsing,
            Parsed,
            Analyzing,
            Analyzed
        }

        private readonly DocumentBuffer _buffer = new DocumentBuffer();
        private readonly DisposeToken _disposeToken = DisposeToken.Create< PythonModule>();
        private IReadOnlyList<DiagnosticsEntry> _parseErrors = Array.Empty<DiagnosticsEntry>();
        private readonly IDiagnosticsService _diagnosticsService;

        private string _documentation; // Must be null initially.
        private CancellationTokenSource _parseCts;
        private CancellationTokenSource _linkedParseCts; // combined with 'dispose' cts
        private Task _parsingTask;
        private PythonAst _ast;

        protected ILogger Log { get; }
        protected IFileSystem FileSystem { get; }
        protected IServiceContainer Services { get; }
        private object AnalysisLock { get; } = new object();
        private State ContentState { get; set; } = State.None;

        protected PythonModule(string name, ModuleType moduleType, IServiceContainer services) {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Services = services ?? throw new ArgumentNullException(nameof(services));
            ModuleType = moduleType;

            Log = services.GetService<ILogger>();
            Interpreter = services.GetService<IPythonInterpreter>();
            Analysis = new EmptyAnalysis(services, this);

            _diagnosticsService = services.GetService<IDiagnosticsService>();
        }

        protected PythonModule(string moduleName, string filePath, ModuleType moduleType, IPythonModule stub, IServiceContainer services) :
            this(new ModuleCreationOptions {
                ModuleName = moduleName,
                FilePath = filePath,
                ModuleType = moduleType,
                Stub = stub
            }, services) { }

        internal PythonModule(ModuleCreationOptions creationOptions, IServiceContainer services)
            : this(creationOptions.ModuleName, creationOptions.ModuleType, services) {
            Check.ArgumentNotNull(nameof(services), services);

            FileSystem = services.GetService<IFileSystem>();
            Location = new LocationInfo(creationOptions.FilePath, creationOptions.Uri, 1, 1);

            var uri = creationOptions.Uri;
            if (uri == null && !string.IsNullOrEmpty(creationOptions.FilePath)) {
                Uri.TryCreate(creationOptions.FilePath, UriKind.Absolute, out uri);
            }
            Uri = uri;
            FilePath = creationOptions.FilePath ?? uri?.LocalPath;
            Stub = creationOptions.Stub;
            if (Stub is PythonModule stub && ModuleType != ModuleType.Stub) {
                stub.PrimaryModule = this;
            }

            if (ModuleType == ModuleType.Specialized || ModuleType == ModuleType.Unresolved) {
                ContentState = State.Analyzed;
            }
            InitializeContent(creationOptions.Content);
        }

        #region IPythonType
        public string Name { get; }
        public virtual IPythonModule DeclaringModule => null;
        public BuiltinTypeId TypeId => BuiltinTypeId.Module;
        public bool IsBuiltin => true;
        public bool IsAbstract => false;
        public virtual bool IsSpecialized => false;

        public IMember CreateInstance(string typeName, LocationInfo location, IArgumentSet args) => this;
        public PythonMemberType MemberType => PythonMemberType.Module;
        public IMember Call(IPythonInstance instance, string memberName, IArgumentSet args) => GetMember(memberName);
        public IMember Index(IPythonInstance instance, object index) => Interpreter.UnknownType;

        public virtual string Documentation {
            get {
                _documentation = _documentation ?? _ast?.Documentation;
                if (_documentation == null) {
                    var m = GetMember("__doc__");
                    _documentation = m.TryGetConstant<string>(out var s) ? s : string.Empty;
                    if (string.IsNullOrEmpty(_documentation)) {
                        m = GetMember($"_{Name}");
                        _documentation = m?.GetPythonType()?.Documentation;
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
        public virtual IMember GetMember(string name) => Analysis.GlobalScope.Variables[name]?.Value;
        public virtual IEnumerable<string> GetMemberNames() {
            // TODO: Filter __all__. See: https://github.com/Microsoft/python-language-server/issues/620

            // drop imported modules and typing.
            return Analysis.GlobalScope.Variables
                .Where(v => !(v.Value?.GetPythonType() is PythonModule)
                            && !(v.Value?.GetPythonType().DeclaringModule is TypingModule && !(this is TypingModule)))
                .Select(v => v.Name);
        }
        #endregion

        #region IPythonFile
        public virtual string FilePath { get; }
        public virtual Uri Uri { get; }
        #endregion

        #region IPythonModule
        public IDocumentAnalysis Analysis { get; private set; }

        public IPythonInterpreter Interpreter { get; }

        /// <summary>
        /// Associated stub module. Note that in case of specialized modules
        /// stub may be actually a real module that is being specialized in code.
        /// </summary>
        public IPythonModule Stub { get; }

        /// <summary>
        /// Global cope of the module.
        /// </summary>
        public IGlobalScope GlobalScope { get; private set; }

        /// <summary>
        /// Ensures that module content is loaded and analysis has started.
        /// Typically module content is loaded at the creation time, but delay
        /// loaded (lazy) modules may choose to defer content retrieval and
        /// analysis until later time, when module members are actually needed.
        /// </summary>
        public async Task LoadAndAnalyzeAsync(CancellationToken cancellationToken = default) {
            InitializeContent(null);
            await GetAstAsync(cancellationToken);
            await Services.GetService<IPythonAnalyzer>().GetAnalysisAsync(this, -1, cancellationToken);
        }

        /// <summary>
        /// If module is a stub points to the primary module.
        /// Typically used in code navigation scenarios when user
        /// wants to see library code and not a stub.
        /// </summary>
        public IPythonModule PrimaryModule { get; internal set; }

        protected virtual string LoadContent() {
            if (ContentState < State.Loading) {
                ContentState = State.Loading;
                try {
                    var code = FileSystem.ReadTextWithRetry(FilePath);
                    ContentState = State.Loaded;
                    return code;
                } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            return null; // Keep content as null so module can be loaded later.
        }

        private void InitializeContent(string content) {
            lock (AnalysisLock) {
                LoadContent(content);

                var startParse = ContentState < State.Parsing && _parsingTask == null;
                if (startParse) {
                    Parse();
                }
            }
        }

        private void LoadContent(string content) {
            if (ContentState < State.Loading) {
                try {
                    content = content ?? LoadContent();
                    _buffer.Reset(0, content);
                    ContentState = State.Loaded;
                } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
        #endregion

        #region ILocatedMember
        public virtual LocationInfo Location { get; }
        #endregion

        #region IDisposable
        public void Dispose() => Dispose(true);

        protected virtual void Dispose(bool disposing) {
            _diagnosticsService?.Remove(Uri);
            _disposeToken.TryMarkDisposed();
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
            return _ast;
        }

        public PythonAst GetAnyAst() => _ast;

        /// <summary>
        /// Provides collection of parsing errors, if any.
        /// </summary>
        public IEnumerable<DiagnosticsEntry> GetParseErrors() => _parseErrors.ToArray();

        public void Update(IEnumerable<DocumentChange> changes) {
            lock (AnalysisLock) {
                _parseCts?.Cancel();
                _parseCts = new CancellationTokenSource();

                _linkedParseCts?.Dispose();
                _linkedParseCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeToken.CancellationToken, _parseCts.Token);

                _buffer.Update(changes);
                Parse();
            }

            Services.GetService<IPythonAnalyzer>().InvalidateAnalysis(this);
        }

        public void Reset(string content) {
            lock (AnalysisLock) {
                if (content != Content) {
                    InitializeContent(content);
                }
            }
        }

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

            var ast = parser.ParseFile();

            //Log?.Log(TraceEventType.Verbose, $"Parse complete: {Name}");

            lock (AnalysisLock) {
                cancellationToken.ThrowIfCancellationRequested();
                if (version != _buffer.Version) {
                    throw new OperationCanceledException();
                }
                _ast = ast;
                _parseErrors = sink?.Diagnostics ?? Array.Empty<DiagnosticsEntry>();

                // Do not report issues with libraries or stubs
                if (sink != null) {
                    _diagnosticsService?.Replace(Uri, _parseErrors);
                }

                ContentState = State.Parsed;
            }

            NewAst?.Invoke(this, EventArgs.Empty);

            if (ContentState < State.Analyzing) {
                ContentState = State.Analyzing;

                var analyzer = Services.GetService<IPythonAnalyzer>();
                analyzer.EnqueueDocumentForAnalysis(this, ast, version, _disposeToken.CancellationToken);
            }

            lock (AnalysisLock) {
                _parsingTask = null;
            }
        }

        private class CollectingErrorSink : ErrorSink {
            private readonly List<DiagnosticsEntry> _diagnostics = new List<DiagnosticsEntry>();

            public IReadOnlyList<DiagnosticsEntry> Diagnostics => _diagnostics;
            public override void Add(string message, SourceSpan span, int errorCode, Severity severity)
                => _diagnostics.Add(new DiagnosticsEntry(message, span, $"parser-{errorCode}", severity));
        }
        #endregion

        #region IAnalyzable
        public void NotifyAnalysisComplete(IDocumentAnalysis analysis) {
            lock (AnalysisLock) {
                if (analysis.Version < Analysis.Version) {
                    return;
                }

                Analysis = analysis;
                GlobalScope = analysis.GlobalScope;

                // Derived classes can override OnAnalysisComplete if they want
                // to perform additional actions on the completed analysis such
                // as declare additional variables, etc.
                OnAnalysisComplete();
                ContentState = State.Analyzed;
            }

            // Do not report issues with libraries or stubs
            if (ModuleType == ModuleType.User) {
                _diagnosticsService?.Replace(Uri, _parseErrors.Concat(analysis.Diagnostics));
            }

            NewAnalysis?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnAnalysisComplete() { }
        #endregion

        #region Analysis
        public IDocumentAnalysis GetAnyAnalysis() => Analysis;

        public Task<IDocumentAnalysis> GetAnalysisAsync(int waitTime = 200, CancellationToken cancellationToken = default)
            => Services.GetService<IPythonAnalyzer>().GetAnalysisAsync(this, waitTime, cancellationToken);

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
                        if (line.EndsWithOrdinal(quote) && line.IndexOf(quote, StringComparison.Ordinal) < line.LastIndexOf(quote, StringComparison.Ordinal)) {
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

        public bool Equals(IPythonModule other) => Name.Equals(other?.Name) && FilePath.Equals(other?.FilePath);
    }
}
