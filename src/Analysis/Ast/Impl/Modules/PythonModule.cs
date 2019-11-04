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
using Microsoft.Python.Analysis.Dependencies;
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
    internal class PythonModule : LocatedMember, IDocument, IAnalyzable, IEquatable<IPythonModule>, IAstNodeContainer, ILocationConverter {
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
        private readonly DisposeToken _disposeToken = DisposeToken.Create<PythonModule>();
        private readonly object _syncObj = new object();
        private IReadOnlyList<DiagnosticsEntry> _parseErrors = Array.Empty<DiagnosticsEntry>();
        private readonly Dictionary<object, Node> _astMap = new Dictionary<object, Node>();
        private readonly IDiagnosticsService _diagnosticsService;

        private string _documentation; // Must be null initially.
        private CancellationTokenSource _parseCts;
        private CancellationTokenSource _linkedParseCts; // combined with 'dispose' cts
        private Task _parsingTask;
        private bool _updated;

        protected ILogger Log { get; }
        protected IFileSystem FileSystem { get; }
        protected IServiceContainer Services { get; }
        private State ContentState { get; set; } = State.None;

        protected PythonModule(string name, ModuleType moduleType, IServiceContainer services) : base(null) {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Services = services ?? throw new ArgumentNullException(nameof(services));
            ModuleType = moduleType;

            Log = services.GetService<ILogger>();
            Interpreter = services.GetService<IPythonInterpreter>();
            Analysis = new EmptyAnalysis(services, this);
            GlobalScope = Analysis.GlobalScope;

            _diagnosticsService = services.GetService<IDiagnosticsService>();
            SetDeclaringModule(this);
        }

        protected PythonModule(string moduleName, string filePath, ModuleType moduleType, IPythonModule stub, bool isTypeshed, IServiceContainer services) :
            this(new ModuleCreationOptions {
                ModuleName = moduleName,
                FilePath = filePath,
                ModuleType = moduleType,
                Stub = stub,
                IsTypeshed = isTypeshed
            }, services) { }

        internal PythonModule(ModuleCreationOptions creationOptions, IServiceContainer services)
            : this(creationOptions.ModuleName, creationOptions.ModuleType, services) {
            Check.ArgumentNotNull(nameof(services), services);

            FileSystem = services.GetService<IFileSystem>();

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

            IsTypeshed = creationOptions.IsTypeshed;

            InitializeContent(creationOptions.Content, 0);
        }

        #region IPythonType
        public string Name { get; }
        public string QualifiedName => Name;
        public BuiltinTypeId TypeId => BuiltinTypeId.Module;
        public bool IsBuiltin => true;
        public bool IsAbstract => false;
        public virtual bool IsSpecialized => false;

        public IPythonInstance CreateInstance(IArgumentSet args) => new PythonInstance(this);
        public override PythonMemberType MemberType => PythonMemberType.Module;
        public IMember Call(IPythonInstance instance, string memberName, IArgumentSet args) => GetMember(memberName);
        public IMember Index(IPythonInstance instance, IArgumentSet args) => Interpreter.UnknownType;

        public virtual string Documentation {
            get {
                _documentation = _documentation ?? this.GetAst()?.Documentation;
                if (_documentation == null) {
                    var m = GetMember("__doc__");
                    _documentation = m.TryGetConstant<string>(out var s) ? s : string.Empty;
                    if (string.IsNullOrEmpty(_documentation)) {
                        m = GetMember($"_{Name}");
                        var t = m?.GetPythonType();
                        _documentation = t != null && !t.Equals(this) ? t.Documentation : null;
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
        public virtual IMember GetMember(string name) => GlobalScope.Variables[name]?.Value;

        public virtual IEnumerable<string> GetMemberNames() {
            // drop imported modules and typing.
            return GlobalScope.Variables
                .Where(v => {
                    // Instances are always fine.
                    if (v.Value is IPythonInstance) {
                        return true;
                    }

                    var valueType = v.Value?.GetPythonType();
                    switch (valueType) {
                        case PythonModule _:
                        case IPythonFunctionType f when f.IsLambda():
                            return false; // Do not re-export modules.
                    }

                    if (this is TypingModule) {
                        return true; // Let typing module behave normally.
                    }

                    // Do not re-export types from typing. However, do export variables
                    // assigned with types from typing. Example:
                    //    from typing import Any # do NOT export Any
                    //    x = Union[int, str] # DO export x
                    return !(valueType?.DeclaringModule is TypingModule) || v.Name != valueType.Name;
                })
                .Select(v => v.Name)
                .ToArray();
        }
        #endregion

        #region ILocatedMember
        public override LocationInfo Definition => Uri != null ? new LocationInfo(Uri.ToAbsolutePath(), Uri) : LocationInfo.Empty;
        #endregion

        #region IPythonModule
        public virtual string FilePath { get; protected set; }
        public virtual Uri Uri { get; }
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
        public IGlobalScope GlobalScope { get; protected set; }

        /// <summary>
        /// If module is a stub points to the primary module.
        /// Typically used in code navigation scenarios when user
        /// wants to see library code and not a stub.
        /// </summary>
        public IPythonModule PrimaryModule { get; private set; }

        /// <summary>
        /// Defines if module belongs to Typeshed and hence resolved
        /// via typeshed module resolution service.
        /// </summary>
        public bool IsTypeshed { get; }
        #endregion

        #region IDisposable
        public void Dispose() => Dispose(true);

        protected virtual void Dispose(bool disposing) {
            _diagnosticsService?.Remove(Uri);
            _disposeToken.TryMarkDisposed();
            var analyzer = Services.GetService<IPythonAnalyzer>();
            analyzer.RemoveAnalysis(this);
            _parseCts?.Dispose();
            _linkedParseCts?.Dispose();
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
        public string Content {
            get {
                lock (_syncObj) {
                    return _buffer.Text;
                }
            }
        }
        #endregion

        #region Parsing
        /// <summary>
        /// Returns document parse tree.
        /// </summary>
        public async Task<PythonAst> GetAstAsync(CancellationToken cancellationToken = default) {
            Task t = null;
            while (true) {
                lock (_syncObj) {
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

        /// <summary>
        /// Provides collection of parsing errors, if any.
        /// </summary>
        public IEnumerable<DiagnosticsEntry> GetParseErrors() => _parseErrors.ToArray();

        public void Update(IEnumerable<DocumentChange> changes) {
            lock (_syncObj) {
                _parseCts?.Cancel();
                _parseCts = new CancellationTokenSource();

                _linkedParseCts?.Dispose();
                _linkedParseCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeToken.CancellationToken, _parseCts.Token);

                _buffer.Update(changes);
                _updated = true;

                Parse();
            }
            Services.GetService<IPythonAnalyzer>().InvalidateAnalysis(this);
        }

        public void Invalidate() {
            lock (_syncObj) {
                ContentState = State.None;
                _buffer.MarkChanged();
                Parse();
            }
            Services.GetService<IPythonAnalyzer>().InvalidateAnalysis(this);
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

            lock (_syncObj) {
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

            lock (_syncObj) {
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
                analyzer.EnqueueDocumentForAnalysis(this, ast, version);
            }

            lock (_syncObj) {
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

        #region IAnalyzable
        public void NotifyAnalysisBegins() {
            lock (_syncObj) {
                if (_updated) {
                    _updated = false;
                    // In all variables find those imported, then traverse imported modules
                    // and remove references to this module. If variable refers to a module,
                    // recurse into module but only process global scope.

                    if (GlobalScope == null) {
                        return;
                    }

                    // TODO: Figure out where the nulls below are coming from.
                    var importedVariables = ((IScope)GlobalScope)
                        .TraverseDepthFirst(c => c?.Children ?? Enumerable.Empty<IScope>())
                        .SelectMany(s => s?.Variables ?? VariableCollection.Empty)
                        .Where(v => v?.Source == VariableSource.Import);

                    foreach (var v in importedVariables) {
                        v.RemoveReferences(this);
                        if (v.Value is IPythonModule module) {
                            RemoveReferencesInModule(module);
                        }
                    }
                }
            }
        }

        public void NotifyAnalysisComplete(IDocumentAnalysis analysis) {
            lock (_syncObj) {
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

                if (ModuleType != ModuleType.User) {
                    _buffer.Clear();
                }
            }

            // Do not report issues with libraries or stubs
            if (ModuleType == ModuleType.User) {
                _diagnosticsService?.Replace(Uri, analysis.Diagnostics, DiagnosticSource.Analysis);
            }

            NewAnalysis?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnAnalysisComplete() { }
        #endregion

        #region IEquatable
        public bool Equals(IPythonModule other) => Name.Equals(other?.Name) && FilePath.Equals(other?.FilePath);
        #endregion

        #region IAstNodeContainer
        public Node GetAstNode(object o) {
            lock (_syncObj) {
                return _astMap.TryGetValue(o, out var n) ? n : null;
            }
        }

        public void AddAstNode(object o, Node n) {
            lock (_syncObj) {
                Debug.Assert(!_astMap.ContainsKey(o) || _astMap[o] == n);
                _astMap[o] = n;
            }
        }

        public void ClearContent() {
            lock (_syncObj) {
                if (ModuleType != ModuleType.User) {
                    _buffer.Clear();
                    _astMap.Clear();
                }
            }
        }
        #endregion

        #region Analysis
        public IDocumentAnalysis GetAnyAnalysis() => Analysis;

        public Task<IDocumentAnalysis> GetAnalysisAsync(int waitTime = 200, CancellationToken cancellationToken = default)
            => Services.GetService<IPythonAnalyzer>().GetAnalysisAsync(this, waitTime, cancellationToken);

        #endregion

        #region Content management
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

        private void InitializeContent(string content, int version) {
            lock (_syncObj) {
                SetOrLoadContent(content);
                if (ContentState < State.Parsing && _parsingTask == null) {
                    Parse();
                }
            }
            Services.GetService<IPythonAnalyzer>().InvalidateAnalysis(this);
        }

        private void SetOrLoadContent(string content) {
            if (ContentState < State.Loading) {
                try {
                    content = content ?? LoadContent();
                    _buffer.SetContent(content);
                    ContentState = State.Loaded;
                } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
        #endregion

        #region Documentation
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

                    if (line != null && quote != null) {
                        // Check if it is a single-liner, but do distinguish from """<eol>
                        // Also, handle quadruple+ quotes.
                        line = line.Trim();
                        line = line.All(c => c == quote[0]) ? quote : line;
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
        #endregion

        #region ILocationConverter
        public virtual SourceLocation IndexToLocation(int index) => this.GetAst()?.IndexToLocation(index) ?? default;
        public virtual int LocationToIndex(SourceLocation location) => this.GetAst()?.LocationToIndex(location) ?? default;
        #endregion

        private void RemoveReferencesInModule(IPythonModule module) {
            if (module.GlobalScope?.Variables != null) {
                foreach (var v in module.GlobalScope.Variables) {
                    v.RemoveReferences(this);
                }
            }
        }
    }
}
