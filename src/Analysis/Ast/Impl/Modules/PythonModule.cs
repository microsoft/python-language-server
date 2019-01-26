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
        protected enum State {
            None,
            Loading,
            Loaded,
            Parsing,
            Parsed,
            Analyzing,
            Analyzed
        }

        private readonly AsyncLocal<bool> _awaiting = new AsyncLocal<bool>();
        private readonly DocumentBuffer _buffer = new DocumentBuffer();
        private readonly CancellationTokenSource _allProcessingCts = new CancellationTokenSource();
        private IReadOnlyList<DiagnosticsEntry> _parseErrors = Array.Empty<DiagnosticsEntry>();

        private string _documentation = string.Empty;
        private TaskCompletionSource<IDocumentAnalysis> _analysisTcs;
        private CancellationTokenSource _linkedAnalysisCts; // cancellation token combined with the 'dispose' cts
        private CancellationTokenSource _parseCts;
        private CancellationTokenSource _linkedParseCts; // combined with 'dispose' cts
        private Task _parsingTask;
        private PythonAst _ast;

        protected ILogger Log { get; }
        protected IFileSystem FileSystem { get; }
        protected IServiceContainer Services { get; }
        protected IDocumentAnalysis Analysis { get; private set; }
        protected object AnalysisLock { get; } = new object();
        protected State ContentState { get; set; } = State.None;

        protected PythonModule(string name, ModuleType moduleType, IServiceContainer services) {
            Check.ArgumentNotNull(nameof(name), name);
            Name = name;
            Services = services;
            ModuleType = moduleType;

            Log = services?.GetService<ILogger>();
            Interpreter = services?.GetService<IPythonInterpreter>();
            Analysis = new EmptyAnalysis(services, this);
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
            // Try __all__ since it contains exported members
            var all = Analysis.GlobalScope.Variables["__all__"];
            if (all?.Value is IPythonCollection collection) {
                return collection.Contents
                    .OfType<IPythonConstant>()
                    .Select(c => c.Value)
                    .OfType<string>()
                    .Where(s => !string.IsNullOrEmpty(s));
            }

            // __all__ is not declared. Try filtering by origin:
            // drop imported modules and generics.
            return Analysis.GlobalScope.Variables
                .Where(v => v.Source == VariableSource.Declaration
                            && v.Value?.MemberType != PythonMemberType.Generic
                            && !(v.Value.GetPythonType() is PythonModule)
                            && !(v.Value.GetPythonType().DeclaringModule is TypingModule && !(this is TypingModule)))
                .Select(v => v.Name);
        }
        #endregion

        #region IPythonFile
        public virtual string FilePath { get; }
        public virtual Uri Uri { get; }
        #endregion

        #region IPythonModule
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
        public virtual Task LoadAndAnalyzeAsync(CancellationToken cancellationToken = default) {
            if (_awaiting.Value) {
                return Task.FromResult(Analysis);
            }
            _awaiting.Value = true;
            InitializeContent(null);
            return GetAnalysisAsync(cancellationToken);
        }

        protected virtual string LoadContent() {
            if (ContentState < State.Loading) {
                ContentState = State.Loading;
                try {
                    var code = FileSystem.ReadAllText(FilePath);
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
                var startAnalysis = startParse | (ContentState < State.Analyzing && _analysisTcs?.Task == null);

                if (startAnalysis) {
                    _analysisTcs = new TaskCompletionSource<IDocumentAnalysis>();
                }

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

        public PythonAst GetAnyAst() => _ast;

        /// <summary>
        /// Provides collection of parsing errors, if any.
        /// </summary>
        public IEnumerable<DiagnosticsEntry> GetParseErrors() => _parseErrors.ToArray();

        public void Update(IEnumerable<DocumentChange> changes) {
            lock (AnalysisLock) {
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

        public void Reset(string content) {
            lock (AnalysisLock) {
                if (content != Content) {
                    InitializeContent(content);
                }
            }
        }

        private void Parse() {
            _awaiting.Value = false;

            _parseCts?.Cancel();
            _parseCts = new CancellationTokenSource();

            _linkedParseCts?.Dispose();
            _linkedParseCts = CancellationTokenSource.CreateLinkedTokenSource(_allProcessingCts.Token, _parseCts.Token);

            ContentState = State.Parsing;
            _parsingTask = Task.Run(() => Parse(_linkedParseCts.Token), _linkedParseCts.Token);
        }

        private void Parse(CancellationToken cancellationToken) {
            var sink = new CollectingErrorSink();
            int version;
            Parser parser;

            //Log?.Log(TraceEventType.Verbose, $"Parse begins: {Name}");

            lock (AnalysisLock) {
                version = _buffer.Version;
                parser = Parser.CreateParser(new StringReader(_buffer.Text), Interpreter.LanguageVersion, new ParserOptions {
                    StubFile = FilePath != null && Path.GetExtension(FilePath).Equals(".pyi", FileSystem.StringComparison),
                    ErrorSink = sink
                });
            }

            var ast = parser.ParseFile();

            //Log?.Log(TraceEventType.Verbose, $"Parse complete: {Name}");

            lock (AnalysisLock) {
                cancellationToken.ThrowIfCancellationRequested();
                if (version != _buffer.Version) {
                    throw new OperationCanceledException();
                }
                _ast = ast;
                _parseErrors = sink.Diagnostics;
                _parsingTask = null;
                ContentState = State.Parsed;
            }

            NewAst?.Invoke(this, EventArgs.Empty);

            if (ContentState < State.Analyzing) {
                Log?.Log(TraceEventType.Verbose, $"Analysis queued: {Name}");
                ContentState = State.Analyzing;

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
                => _diagnostics.Add(new DiagnosticsEntry(message, span, $"parser-{errorCode}", severity));
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
            lock (AnalysisLock) {
                // The notification comes from the analyzer when it needs to invalidate
                // current analysis since one of the dependencies changed. Upon text
                // buffer change the version may be incremented twice - once in Update()
                // and then here. This is normal.
                ExpectedAnalysisVersion++;
                _analysisTcs = _analysisTcs ?? new TaskCompletionSource<IDocumentAnalysis>();
                //Log?.Log(TraceEventType.Verbose, $"Analysis pending: {Name}");
            }
        }

        public virtual bool NotifyAnalysisComplete(IDocumentAnalysis analysis) {
            lock (AnalysisLock) {
                // Log?.Log(TraceEventType.Verbose, $"Analysis complete: {Name}, Version: {analysis.Version}, Expected: {ExpectedAnalysisVersion}");
                if (analysis.Version == ExpectedAnalysisVersion) {
                    Analysis = analysis;
                    GlobalScope = analysis.GlobalScope;

                    // Derived classes can override OnAnalysisComplete if they want
                    // to perform additional actions on the completed analysis such
                    // as declare additional variables, etc.
                    OnAnalysisComplete();

                    _analysisTcs.TrySetResult(analysis);
                    _analysisTcs = null;
                    ContentState = State.Analyzed;

                    NewAnalysis?.Invoke(this, EventArgs.Empty);
                    return true;
                }
                Debug.Assert(ExpectedAnalysisVersion > analysis.Version);
                return false;
            }
        }

        protected virtual void OnAnalysisComplete() { }
        #endregion

        #region Analysis
        public IDocumentAnalysis GetAnyAnalysis() => Analysis;

        public Task<IDocumentAnalysis> GetAnalysisAsync(CancellationToken cancellationToken = default) {
            lock (AnalysisLock) {
                return _analysisTcs?.Task ?? Task.FromResult(Analysis);
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

        /// <summary>
        /// Provides ability to specialize function return type manually.
        /// </summary>
        protected void SpecializeFunction(string name, IMember returnValue) {
            var f = GetOrCreateFunction(name);
            if (f != null) {
                foreach (var o in f.Overloads.OfType<PythonFunctionOverload>()) {
                    o.SetReturnValue(returnValue, true);
                }
            }
        }

        /// <summary>
        /// Provides ability to dynamically calculate function return type.
        /// </summary>
        internal void SpecializeFunction(string name, ReturnValueProvider returnTypeCallback) {
            var f = GetOrCreateFunction(name);
            if (f != null) {
                foreach (var o in f.Overloads.OfType<PythonFunctionOverload>()) {
                    o.SetReturnValueProvider(returnTypeCallback);
                }
                f.Specialize();
            }
        }

        private PythonFunctionType GetOrCreateFunction(string name) {
            var f = Analysis.GlobalScope.Variables[name]?.Value as PythonFunctionType;
            // We DO want to replace class by function. Consider type() in builtins.
            // 'type()' in code is a function call, not a type class instantiation.
            if (f == null) {
                f = PythonFunctionType.ForSpecialization(name, this);
                f.AddOverload(new PythonFunctionOverload(name, this, LocationInfo.Empty));
                Analysis.GlobalScope.DeclareVariable(name, f, VariableSource.Declaration, LocationInfo.Empty);
            }
            return f;
        }

        public bool Equals(IPythonModule other) => Name.Equals(other?.Name) && FilePath.Equals(other?.FilePath);
    }
}
