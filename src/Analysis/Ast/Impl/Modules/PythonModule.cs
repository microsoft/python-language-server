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
using System.Linq;
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
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Modules {
    /// <summary>
    /// Primary base for all modules and user documents. Provides access
    /// to AST and the module analysis.
    /// </summary>
    [DebuggerDisplay("{Name} : {ModuleType}")]
    internal partial class PythonModule : LocatedMember, IDocument, IAnalyzable, IEquatable<IPythonModule>, IAstNodeContainer {
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
        private IReadOnlyList<DiagnosticsEntry> _parseErrors = Array.Empty<DiagnosticsEntry>();
        private readonly Dictionary<object, Node> _astMap = new Dictionary<object, Node>();
        private readonly IDiagnosticsService _diagnosticsService;

        private string _documentation; // Must be null initially.
        private CancellationTokenSource _parseCts;
        private CancellationTokenSource _linkedParseCts; // combined with 'dispose' cts
        private Task _parsingTask;
        private bool _updated;
        private string _qualifiedName;

        protected ILogger Log { get; }
        protected IFileSystem FileSystem { get; }
        protected IServiceContainer Services { get; }
        private object AnalysisLock { get; } = new object();
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
            InitializeContent(creationOptions.Content, 0);
        }

        #region IPythonType
        public string Name { get; }

        public virtual string QualifiedName {
            get {
                if (string.IsNullOrEmpty(FilePath) || ModuleType == ModuleType.User) {
                    return Name;
                }
                return string.IsNullOrEmpty(_qualifiedName) 
                    ? _qualifiedName = this.CalculateQualifiedName(FileSystem) : _qualifiedName;
            }
        }

        public BuiltinTypeId TypeId => BuiltinTypeId.Module;
        public bool IsBuiltin => true;
        public bool IsAbstract => false;
        public virtual bool IsSpecialized => false;

        public IMember CreateInstance(string typeName, IArgumentSet args) => this;
        public override PythonMemberType MemberType => PythonMemberType.Module;
        public IMember Call(IPythonInstance instance, string memberName, IArgumentSet args) => GetMember(memberName);
        public IMember Index(IPythonInstance instance, object index) => Interpreter.UnknownType;

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
                    if (valueType is PythonModule) {
                        return false; // Do not re-export modules.
                    }
                    // Do not re-export types from typing
                    return !(valueType?.DeclaringModule is TypingModule) || this is TypingModule;
                })
                .Select(v => v.Name);
        }
        #endregion

        #region ILocatedMember
        public override LocationInfo Definition => new LocationInfo(Uri.ToAbsolutePath(), Uri, 0, 0);
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
        public virtual IGlobalScope GlobalScope { get; protected set; }

        /// <summary>
        /// If module is a stub points to the primary module.
        /// Typically used in code navigation scenarios when user
        /// wants to see library code and not a stub.
        /// </summary>
        public IPythonModule PrimaryModule { get; private set; }

        #endregion

        #region IDisposable
        public void Dispose() => Dispose(true);

        protected virtual void Dispose(bool disposing) {
            _diagnosticsService?.Remove(Uri);
            _disposeToken.TryMarkDisposed();
            var analyzer = Services.GetService<IPythonAnalyzer>();
            analyzer.RemoveAnalysis(this);
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

        #region IEquatable
        public bool Equals(IPythonModule other) => Name.Equals(other?.Name) && FilePath.Equals(other?.FilePath);
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
