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

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    [DebuggerDisplay("{Module.Name} : {Module.ModuleType}")]
    internal class ModuleWalker : AnalysisWalker {
        private readonly IDocumentAnalysis _stubAnalysis;

        public ModuleWalker(IServiceContainer services, IPythonModule module, PythonAst ast)
            : base(services, module, ast) {
            _stubAnalysis = Module.Stub is IDocument doc ? doc.GetAnyAnalysis() : null;
        }

        public override async Task<bool> WalkAsync(PythonAst node, CancellationToken cancellationToken = default) {
            Check.InvalidOperation(() => Ast == node, "walking wrong AST");

            // Collect basic information about classes and functions in order
            // to correctly process forward references. Does not determine
            // types yet since at this time imports or generic definitions
            // have not been processed.
            await SymbolTable.BuildAsync(Eval, cancellationToken);
            if (_stubAnalysis != null) {
                Eval.Log?.Log(TraceEventType.Information, $"'{Module.Name}' evaluation skipped, stub is available.");
            }
            return await base.WalkAsync(node, cancellationToken);
        }

        // Classes and functions are walked by their respective evaluators
        public override async Task<bool> WalkAsync(ClassDefinition node, CancellationToken cancellationToken = default) {
            // Don't evaluate if there is a stub definition available
            if (_stubAnalysis == null) {
                await SymbolTable.EvaluateAsync(node, cancellationToken);
            }
            return false;
        }

        public override async Task<bool> WalkAsync(FunctionDefinition node, CancellationToken cancellationToken = default) {
            if (_stubAnalysis == null) {
                await SymbolTable.EvaluateAsync(node, cancellationToken);
            }
            return false;
        }

        public async Task<IGlobalScope> CompleteAsync(CancellationToken cancellationToken = default) {
            if (_stubAnalysis == null) {
                await SymbolTable.EvaluateAllAsync(cancellationToken);
                SymbolTable.ReplacedByStubs.Clear();
            }
            MergeStub();
            return Eval.GlobalScope;
        }

        /// <summary>
        /// Merges data from stub with the data from the module.
        /// </summary>
        /// <remarks>
        /// Functions are taken from the stub by the function walker since
        /// information on the return type is needed during the analysis walk.
        /// However, if the module is compiled (scraped), it often lacks some
        /// of the definitions. Stub may contains those so we need to merge it in.
        /// </remarks>
        private void MergeStub() {
            if (Module.ModuleType == ModuleType.User) {
                return;
            }
            // No stub, no merge.
            if (_stubAnalysis == null) {
                return;
            }

            // Note that scrape can pick up more functions than the stub contains
            // Or the stub can have definitions that scraping had missed. Therefore
            // merge is the combination of the two with the documentation coming
            // from the library source of from the scraped module.
            foreach (var v in _stubAnalysis.TopLevelVariables) {
                var stubType = v.Value.GetPythonType();
                if (stubType.IsUnknown()) {
                    continue;
                }

                var sourceVar = Eval.GlobalScope.Variables[v.Name];
                var srcType = sourceVar?.Value.GetPythonType();

                // If types are the classes, merge members.
                // Otherwise, replace type from one from the stub.

                if (srcType is PythonClassType cls) {
                    // If class exists, add or replace its members
                    // with ones from the stub, preserving documentation.
                    foreach (var name in stubType.GetMemberNames()) {
                        var stubMember = stubType.GetMember(name);
                        var member = cls.GetMember(name);

                        // Get documentation from the current type, if any, since stubs
                        // typically do not contain documentation while scraped code does.
                        if (member != null) {
                            var documentation = member.GetPythonType()?.Documentation;
                            if (!string.IsNullOrEmpty(documentation)) {
                                stubMember.GetPythonType<PythonType>()?.SetDocumentationProvider(_ => documentation);
                            }
                        }
                        cls.AddMember(name, stubMember, overwrite: true);
                    }
                } else {
                    // Re-declare variable with the data from the stub.
                    if (!stubType.IsUnknown()) {
                        if (srcType != null) {
                            (stubType as PythonType)?.TrySetTypeId(srcType.TypeId);
                        }
                        // TODO: choose best type between the scrape and the stub. Stub probably should always win.
                        Eval.DeclareVariable(v.Name, v.Value, LocationInfo.Empty, overwrite: true);
                    }
                }
            }
        }
    }
}
