// Python Tools for Visual Studio
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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// Provides interactions to analysis a single file in a project and get the results back.
    /// 
    /// To analyze a file the tree should be updated with a call to UpdateTree and then PreParse
    /// should be called on all files.  Finally Parse should then be called on all files.
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable",
        Justification = "Unclear ownership makes it unlikely this object will be disposed correctly")]
    internal sealed class ProjectEntry : IPythonProjectEntry, IAggregateableProjectEntry, IDocument {
        private readonly SortedDictionary<int, DocumentBuffer> _buffers;
        private readonly ConcurrentQueue<WeakReference<ReferenceDict>> _backReferences = new ConcurrentQueue<WeakReference<ReferenceDict>>();
        private readonly HashSet<AggregateProjectEntry> _aggregates = new HashSet<AggregateProjectEntry>();

        private TaskCompletionSource<IModuleAnalysis> _analysisTcs = new TaskCompletionSource<IModuleAnalysis>();
        private AnalysisUnit _unit;
        private readonly ManualResetEventSlim _pendingParse = new ManualResetEventSlim(true);
        private long _expectedParseVersion;
        private long _expectedAnalysisVersion;

        internal ProjectEntry(
            PythonAnalyzer state,
            string moduleName,
            string filePath,
            Uri documentUri,
            IAnalysisCookie cookie
        ) {
            ProjectState = state;
            ModuleName = moduleName ?? "";
            DocumentUri = documentUri ?? MakeDocumentUri(filePath);
            FilePath = filePath;
            Cookie = cookie;

            MyScope = new ModuleInfo(ModuleName, this, state.Interpreter.CreateModuleContext());
            _unit = new AnalysisUnit(null, MyScope.Scope);

            _buffers = new SortedDictionary<int, DocumentBuffer> { [0] = new DocumentBuffer() };
            if (Cookie is InitialContentCookie c) {
                _buffers[0].Reset(c.Version, c.Content);
            }
            AnalysisLog.NewUnit(_unit);
        }

        internal static Uri MakeDocumentUri(string filePath) {
            Uri u;
            if (!Path.IsPathRooted(filePath)) {
                u = new Uri("file:///LOCAL-PATH/{0}".FormatInvariant(filePath.Replace('\\', '/')));
            } else {
                u = new Uri(filePath);
            }

            return u;
        }

        public event EventHandler<EventArgs> NewParseTree;
        public event EventHandler<EventArgs> NewAnalysis;

        private class ActivePythonParse : IPythonParse {
            private readonly ProjectEntry _entry;
            private readonly long _expectedVersion;
            private bool _completed;

            public ActivePythonParse(ProjectEntry entry, long expectedVersion) {
                _entry = entry;
                _expectedVersion = expectedVersion;
            }

            public PythonAst Tree { get; set; }
            public IAnalysisCookie Cookie { get; set; }

            public void Dispose() {
                if (_completed) {
                    return;
                }
                lock (_entry) {
                    if (_entry._expectedParseVersion == _expectedVersion) {
                        _entry._pendingParse.Set();
                    }
                }
            }

            public void Complete() {
                _completed = true;
                lock (_entry) {
                    if (_entry._expectedParseVersion == _expectedVersion) {
                        _entry.SetCurrentParse(Tree, Cookie);
                        _entry._pendingParse.Set();
                    }
                }
            }
        }

        public IPythonParse BeginParse() {
            _pendingParse.Reset();
            lock (this) {
                _expectedParseVersion += 1;
                return new ActivePythonParse(this, _expectedParseVersion);
            }
        }

        public IPythonParse GetCurrentParse() {
            lock (this) {
                return new StaticPythonParse(Tree, Cookie);
            }
        }

        internal Task<IModuleAnalysis> GetAnalysisAsync(int waitingTimeout = -1, CancellationToken cancellationToken = default(CancellationToken)) {
            Task<IModuleAnalysis> task;
            lock (this) {
                task = _analysisTcs.Task;
            }

            if (task.IsCompleted || waitingTimeout == -1 && !cancellationToken.CanBeCanceled) {
                return task;
            }

            var timeoutTask = Task.Delay(waitingTimeout, cancellationToken).ContinueWith(t => {
                lock (this) {
                    return Analysis;
                }
            }, cancellationToken);

            return Task.WhenAny(timeoutTask, task).Unwrap();
        }

        internal void SetCompleteAnalysis() {
            lock (this) {
                if (_expectedAnalysisVersion != Analysis.Version) {
                    return;
                }
                _analysisTcs.TrySetResult(Analysis);
            }
            RaiseNewAnalysis();
        }

        internal void ResetCompleteAnalysis() {
            TaskCompletionSource<IModuleAnalysis> analysisTcs = null;
            lock (this) {
                _expectedAnalysisVersion = AnalysisVersion + 1;
                analysisTcs = _analysisTcs;
                _analysisTcs = new TaskCompletionSource<IModuleAnalysis>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            analysisTcs?.TrySetCanceled();
        }

        public void SetCurrentParse(PythonAst tree, IAnalysisCookie cookie, bool notify = true) {
            lock (this) {
                Tree = tree;
                Cookie = cookie;
            }
            if (notify) {
                NewParseTree?.Invoke(this, EventArgs.Empty);
            }
        }

        public IPythonParse WaitForCurrentParse(int timeout = Timeout.Infinite, CancellationToken token = default(CancellationToken)) {
            if (!_pendingParse.Wait(timeout, token)) {
                return null;
            }
            return GetCurrentParse();
        }

        internal bool IsVisible(ProjectEntry assigningScope) => true;

        public void Analyze(CancellationToken cancel) => Analyze(cancel, false);

        public void Analyze(CancellationToken cancel, bool enqueueOnly) {
            if (cancel.IsCancellationRequested) {
                return;
            }

            lock (this) {
                AnalysisVersion++;

                foreach (var aggregate in _aggregates) {
                    aggregate?.BumpVersion();
                }

                Parse(enqueueOnly, cancel);
            }

            if (!enqueueOnly) {
                RaiseNewAnalysis();
            }
        }

        private void RaiseNewAnalysis() => NewAnalysis?.Invoke(this, EventArgs.Empty);

        public int AnalysisVersion { get; private set; }

        public bool IsAnalyzed => Analysis != null;

        private void Parse(bool enqueueOnly, CancellationToken cancel) {
#if DEBUG
            Debug.Assert(Monitor.IsEntered(this));
#endif
            var parse = GetCurrentParse();
            var tree = parse?.Tree;
            var cookie = parse?.Cookie;
            if (tree == null) {
                return;
            }

            var oldParent = MyScope.ParentPackage;
            if (FilePath != null) {
                ProjectState.ModulesByFilename[FilePath] = MyScope;
            }

            if (oldParent != null) {
                // update us in our parent package
                oldParent.AddChildPackage(MyScope, _unit);
            } else if (FilePath != null) {
                // we need to check and see if our parent is a package for the case where we're adding a new
                // file but not re-analyzing our parent package.
                string parentFilename;
                if (ModulePath.IsInitPyFile(FilePath)) {
                    // subpackage
                    parentFilename = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(FilePath)), "__init__.py");
                } else {
                    // just a module
                    parentFilename = Path.Combine(Path.GetDirectoryName(FilePath), "__init__.py");
                }

                ModuleInfo parentPackage;
                if (ProjectState.ModulesByFilename.TryGetValue(parentFilename, out parentPackage)) {
                    parentPackage.AddChildPackage(MyScope, _unit);
                }
            }

            _unit = new AnalysisUnit(tree, MyScope.Scope);
            AnalysisLog.NewUnit(_unit);

            MyScope.Scope.Children = new List<InterpreterScope>();
            MyScope.Scope.ClearNodeScopes();
            MyScope.Scope.ClearNodeValues();
            MyScope.Scope.ClearLinkedVariables();
            MyScope.Scope.ClearVariables();
            MyScope.ClearReferencedModules();
            MyScope.ClearUnresolvedModules();
            _unit.State.ClearDiagnostics(this);

            MyScope.EnsureModuleVariables(_unit.State);

            foreach (var value in MyScope.Scope.AllVariables) {
                value.Value.EnqueueDependents();
            }

            // collect top-level definitions first
            var walker = new OverviewWalker(this, _unit, tree);
            tree.Walk(walker);

            MyScope.Specialize();

            // It may be that we have analyzed some child packages of this package already, but because it wasn't analyzed,
            // the children were not registered. To handle this possibility, scan analyzed packages for children of this
            // package (checked by module name first, then sanity-checked by path), and register any that match.
            if (ModulePath.IsInitPyFile(FilePath)) {
                string pathPrefix = PathUtils.EnsureEndSeparator(Path.GetDirectoryName(FilePath));
                var children =
                    from pair in ProjectState.ModulesByFilename
                        // Is the candidate child package in a subdirectory of our package?
                    let fileName = pair.Key
                    where fileName.StartsWithOrdinal(pathPrefix, ignoreCase: true)
                    let moduleName = pair.Value.Name
                    // Is the full name of the candidate child package qualified with the name of our package?
                    let lastDot = moduleName.LastIndexOf('.')
                    where lastDot > 0
                    let parentModuleName = moduleName.Substring(0, lastDot)
                    where parentModuleName == MyScope.Name
                    select pair.Value;
                foreach (var child in children) {
                    MyScope.AddChildPackage(child, _unit);
                }
            }

            _unit.Enqueue();

            if (!enqueueOnly) {
                ProjectState.AnalyzeQueuedEntries(cancel);
            }

            // publish the analysis now that it's complete/running
            Analysis = new ModuleAnalysis(
                _unit,
                ((ModuleScope)_unit.Scope).CloneForPublish(),
                DocumentUri,
                AnalysisVersion
            );
        }

        public IGroupableAnalysisProject AnalysisGroup => ProjectState;

        #region IPythonProjectEntry
        public IModuleAnalysis Analysis { get; private set; }

        public string FilePath { get; }

        public IAnalysisCookie Cookie { get; private set; }

        public PythonAnalyzer ProjectState { get; private set; }

        public PythonAst Tree { get; private set; }

        internal ModuleInfo MyScope { get; private set; }

        public IModuleContext AnalysisContext => MyScope.InterpreterContext;

        public string ModuleName { get; }

        public Uri DocumentUri { get; }

        public Dictionary<object, object> Properties { get; } = new Dictionary<object, object>();

        public IDocument Document => this;
        #endregion

        public void Dispose() {
            lock (this) {
                AnalysisVersion = -1;

                var state = ProjectState;
                foreach (var aggregatedInto in _aggregates) {
                    if (aggregatedInto != null && aggregatedInto.AnalysisVersion != -1) {
                        state?.ClearAggregate(aggregatedInto);
                        aggregatedInto.RemovedFromProject();
                    }
                }

                while (_backReferences.TryDequeue(out var reference)) {
                    if (reference.TryGetTarget(out var referenceDict)) {
                        lock (referenceDict) {
                            referenceDict.Remove(this);
                        }
                    }
                }

                MyScope.ClearReferencedModules();
                NewParseTree -= NewParseTree;
                NewAnalysis -= NewAnalysis;
            }
        }

        internal void Enqueue() {
            _unit.Enqueue();
        }

        public void AggregatedInto(IVersioned into) {
            if (into is AggregateProjectEntry agg) {
                lock (this) {
                    _aggregates.Add(agg);
                }
            }
        }

        public IEnumerable<int> DocumentParts => _buffers.Keys.AsLockedEnumerable(_buffers);

        public Stream ReadDocumentBytes(int part, out int version) {
            lock (_buffers) {
                if (_buffers[part].Version >= 0) {
                    version = _buffers[part].Version;
                    return EncodeToStream(_buffers[part].Text, Encoding.UTF8);
                } else if (part == 0) {
                    var s = PathUtils.OpenWithRetry(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    version = -1;
                    return s;
                }
                version = -1;
                return null;
            }
        }

        internal static Stream EncodeToStream(StringBuilder text, Encoding encoding, int chunkSize = 4096) {
            byte[] bytes, preamble;
            MemoryStream ms;

            if (text.Length < chunkSize) {
                preamble = encoding.GetPreamble() ?? Array.Empty<byte>();
                bytes = encoding.GetBytes(text.ToString());
                if (preamble.Any()) {
                    ms = new MemoryStream(preamble.Length + bytes.Length);
                    ms.Write(preamble, 0, preamble.Length);
                    ms.Write(bytes, 0, bytes.Length);
                    ms.Seek(0, SeekOrigin.Begin);
                    return ms;
                }
                return new MemoryStream(bytes);
            }

            // Estimate 1 byte per character (or 2 bytes for UTF-16) for initial allocation
            ms = new MemoryStream(Encoding.Unicode.Equals(encoding) ? text.Length * 2 : text.Length);
            var enc = encoding.GetEncoder();
            var chars = new char[chunkSize];

            preamble = encoding.GetPreamble() ?? Array.Empty<byte>();
            ms.Write(preamble, 0, preamble.Length);

            bytes = new byte[encoding.GetMaxByteCount(chunkSize)];
            for (int i = 0; i < text.Length;) {
                bool flush = true;
                int len = text.Length - i;
                if (len > chars.Length) {
                    flush = false;
                    len = chars.Length;
                }
                text.CopyTo(i, chars, 0, len);
                enc.Convert(chars, 0, len, bytes, 0, bytes.Length, flush, out int charCount, out int bytesUsed, out _);
                i += charCount;
                ms.Write(bytes, 0, bytesUsed);
            }
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }

        public TextReader ReadDocument(int part, out int version) {
            var stream = ReadDocumentBytes(part, out version);
            if (stream == null) {
                version = -1;
                return null;
            }
            try {
                var sr = Parser.ReadStreamWithEncoding(stream, ProjectState.LanguageVersion);
                stream = null;
                return sr;
            } finally {
                stream?.Dispose();
            }
        }

        public int GetDocumentVersion(int part) {
            lock (_buffers) {
                if (_buffers.TryGetValue(part, out var buffer)) {
                    return buffer.Version;
                }
                return -1;
            }
        }

        public void UpdateDocument(int part, DocumentChangeSet changes) {
            lock (_buffers) {
                if (!_buffers.TryGetValue(part, out var buffer)) {
                    _buffers[part] = buffer = new DocumentBuffer();
                }
                int versionBefore = buffer.Version;
                buffer.Update(changes);

                // Reset the current cookie if the version did not increase
                if (buffer.Version <= versionBefore) {
                    SetCurrentParse(Tree, null, false);
                }
            }
        }

        public void ResetDocument(int version, string content) {
            lock (_buffers) {
                _buffers.Clear();
                _buffers[0] = new DocumentBuffer();
                _buffers[0].Reset(version, content);
                SetCurrentParse(Tree, null, false);
            }
        }

        public void AddBackReference(ReferenceDict referenceDict) {
            _backReferences.Enqueue(new WeakReference<ReferenceDict>(referenceDict));
        }
    }

    class InitialContentCookie : IAnalysisCookie {
        public string Content { get; set; }
        public int Version { get; set; }
    }


    sealed class StaticPythonParse : IPythonParse {
        public StaticPythonParse(PythonAst tree, IAnalysisCookie cookie) {
            Tree = tree;
            Cookie = cookie;
        }

        public PythonAst Tree { get; set; }
        public IAnalysisCookie Cookie { get; set; }
        public void Dispose() { }
        public void Complete() => throw new NotSupportedException();
    }
}
