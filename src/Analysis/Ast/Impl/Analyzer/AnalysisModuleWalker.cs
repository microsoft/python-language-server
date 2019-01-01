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
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    [DebuggerDisplay("{Module.Name} : {Module.ModuleType}")]
    internal class AnalysisModuleWalker : AnalysisWalker {
        public AnalysisModuleWalker(IServiceContainer services, IPythonModule module, PythonAst ast)
            : base(services, module, ast) { }

        public override async Task<bool> WalkAsync(PythonAst node, CancellationToken cancellationToken = default) {
            Check.InvalidOperation(() => Ast == node, "walking wrong AST");

            // Collect basic information about classes and functions in order
            // to correctly process forward references. Does not determine
            // types yet since at this time imports or generic definitions
            // have not been processed.
            CollectTopLevelDefinitions();
            return await base.WalkAsync(node, cancellationToken);
        }

        private void CollectTopLevelDefinitions() {
            var statement = (Ast.Body as SuiteStatement)?.Statements.ToArray() ?? Array.Empty<Statement>();

            foreach (var node in statement.OfType<FunctionDefinition>()) {
                AddFunction(node, null, Eval.GetLoc(node));
            }

            foreach (var node in statement.OfType<ClassDefinition>()) {
                var classInfo = CreateClass(node);
                Eval.DeclareVariable(node.Name, classInfo, GetLoc(node));
            }
        }

        public override async Task<IGlobalScope> CompleteAsync(CancellationToken cancellationToken = default) {
            var gs = await base.CompleteAsync(cancellationToken);
            MergeStub();
            return gs;
        }

        public override async Task<bool> WalkAsync(ClassDefinition node, CancellationToken cancellationToken = default) {
            var classWalker = new AnalysisClassWalker(this, node);
            await classWalker.WalkAsync(cancellationToken);
            return false;
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
            // We replace classes only in compiled (scraped) modules.
            // Stubs do not apply to user code and library modules that come in source
            // should be providing sufficient information on their classes from the code.
            if (Module.ModuleType != ModuleType.Compiled) {
                return;
            }

            // No stub, no merge.
            IDocumentAnalysis stubAnalysis = null;
            if (Module.Stub is IDocument doc) {
                stubAnalysis = doc.GetAnyAnalysis();
            }
            if (stubAnalysis == null) {
                return;
            }

            // Note that scrape can pick up more functions than the stub contains
            // Or the stub can have definitions that scraping had missed. Therefore
            // merge is the combination of the two with documentation coming from scrape.
            foreach (var v in stubAnalysis.TopLevelVariables) {
                var currentVar = Eval.GlobalScope.Variables[v.Name];

                var stub = v.Value.GetPythonType<PythonClassType>();
                if (stub == null) {
                    continue;
                }

                var cls = currentVar.GetPythonType<PythonClassType>();
                if (cls != null) {
                    // If class exists, add or replace its members
                    // with ones from the stub, preserving documentation.
                    foreach (var name in stub.GetMemberNames()) {
                        var stubMember = stub.GetMember(name);
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
                    if (currentVar != null && currentVar.Value.IsUnknown() && !v.Value.IsUnknown()) {
                        // TODO: choose best type between the scrape and the stub. Stub probably should always win.
                        Eval.DeclareVariable(v.Name, v.Value, LocationInfo.Empty, overwrite: true);
                    }
                }
            }
        }

        public PythonClassType CreateClass(ClassDefinition node) {
            node = node ?? throw new ArgumentNullException(nameof(node));
            return new PythonClassType(
                node,
                Module,
                GetDoc(node.Body as SuiteStatement),
                GetLoc(node),
                Interpreter,
                Eval.SuppressBuiltinLookup ? BuiltinTypeId.Unknown : BuiltinTypeId.Type); // built-ins set type later
        }

    }
}
