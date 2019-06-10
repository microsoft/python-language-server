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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Modules {
    /// <summary>
    /// Primary base for all modules and user documents. Provides access
    /// to AST and the module analysis.
    /// </summary>
    [DebuggerDisplay("{Name} : {ModuleType}")]
    internal partial class PythonModule {
       public void NotifyAnalysisBegins() {
            lock (AnalysisLock) {
                if (Analysis is LibraryAnalysis) {
                    var sw = Log != null ? Stopwatch.StartNew() : null;
                    lock (AnalysisLock) {
                        _astMap[this] = RecreateAst();
                    }
                    sw?.Stop();
                    Log?.Log(TraceEventType.Verbose, $"Reloaded AST of {Name} in {sw?.Elapsed.TotalMilliseconds} ms");
                }

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

        public void NotifyAnalysisComplete(int version, ModuleWalker walker, bool isFinalPass) {
            lock (AnalysisLock) {
                if (version < Analysis.Version) {
                    return;
                }

                Analysis = CreateAnalysis(version, walker, isFinalPass);
                GlobalScope = Analysis.GlobalScope;

                // Derived classes can override OnAnalysisComplete if they want
                // to perform additional actions on the completed analysis such
                // as declare additional variables, etc.
                OnAnalysisComplete();
                ContentState = State.Analyzed;

                if (ModuleType != ModuleType.User) {
                    _buffer.Reset(_buffer.Version, string.Empty);
                }
            }

            // Do not report issues with libraries or stubs
            if (ModuleType == ModuleType.User) {
                _diagnosticsService?.Replace(Uri, Analysis.Diagnostics, DiagnosticSource.Analysis);
            }

            NewAnalysis?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnAnalysisComplete() { }

        public IDocumentAnalysis GetAnyAnalysis() => Analysis;

        public Task<IDocumentAnalysis> GetAnalysisAsync(int waitTime = 200, CancellationToken cancellationToken = default)
            => Services.GetService<IPythonAnalyzer>().GetAnalysisAsync(this, waitTime, cancellationToken);

        private IDocumentAnalysis CreateAnalysis(int version, ModuleWalker walker, bool isFinalPass)
            => ModuleType == ModuleType.Library && isFinalPass
                ? new LibraryAnalysis(this, version, walker.Eval.Services, walker.GlobalScope, walker.StarImportMemberNames)
                : (IDocumentAnalysis)new DocumentAnalysis(this, version, walker.GlobalScope, walker.Eval, walker.StarImportMemberNames);
    }
}
