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

using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;
using ErrorCodes = Microsoft.Python.Analysis.Diagnostics.ErrorCodes;

namespace Microsoft.Python.Analysis.Analyzer.Handlers {
    internal sealed class NonLocalHandler : StatementHandler {
        public NonLocalHandler(AnalysisWalker walker) : base(walker) { }

        public bool HandleNonLocal(NonlocalStatement node) {
            foreach (var nex in node.Names) {
                var m = Eval.LookupNameInScopes(nex.Name, out _, out var v, LookupOptions.Nonlocal);
                if (m != null) {
                    Eval.CurrentScope.DeclareNonLocal(nex.Name, nex);
                    v?.AddReference(Module, nex);
                } else {
                    Eval.ReportDiagnostics(Eval.Module.Uri,
                        new DiagnosticsEntry(
                            Resources.ErrorVariableNotDefinedGlobally.FormatInvariant(nex.Name),
                            Eval.GetLoc(nex).Span, ErrorCodes.VariableNotDefinedNonLocal, Severity.Warning
                        ));
                }
            }
            return false;
        }

        public bool HandleGlobal(GlobalStatement node) {
            foreach (var nex in node.Names) {
                var m = Eval.LookupNameInScopes(nex.Name, out _, out var v, LookupOptions.Global);
                if (m != null) {
                    Eval.CurrentScope.DeclareGlobal(nex.Name, nex);
                    v?.AddReference(Module, nex);
                } else {
                    Eval.ReportDiagnostics(Eval.Module.Uri, 
                        new DiagnosticsEntry(
                            Resources.ErrorVariableNotDefinedGlobally.FormatInvariant(nex.Name),
                            Eval.GetLoc(nex).Span, ErrorCodes.VariableNotDefinedGlobally, Severity.Warning
                        ));
                }
            }
            return false;
        }
    }
}
