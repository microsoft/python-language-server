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

using System.Collections.Generic;
using System.Linq;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;
using ErrorCodes = Microsoft.Python.Analysis.Diagnostics.ErrorCodes;

namespace Microsoft.Python.Analysis.Linting.UndefinedVariables {
    internal sealed class UndefinedVariablesWalker : LinterWalker {
        private readonly List<DiagnosticsEntry> _diagnostics = new List<DiagnosticsEntry>();
        private readonly ExpressionWalker _ew;

        public UndefinedVariablesWalker(IDocumentAnalysis analysis, IServiceContainer services)
            : base(analysis, services) {
            _ew = new ExpressionWalker(this);
        }

        public IReadOnlyList<DiagnosticsEntry> Diagnostics => _diagnostics;

        public override bool Walk(SuiteStatement node) {
            foreach (var statement in node.Statements) {
                switch (statement) {
                    case ClassDefinition cd:
                        HandleScope(cd);
                        break;
                    case FunctionDefinition fd:
                        HandleScope(fd);
                        break;
                    case GlobalStatement gs:
                        HandleGlobal(gs);
                        break;
                    case NonlocalStatement nls:
                        HandleNonLocal(nls);
                        break;
                    default:
                        statement.Walk(_ew);
                        break;
                }
            }
            return false;
        }

        public void ReportUndefinedVariable(NameExpression node) {
            var eval = Analysis.ExpressionEvaluator;
            ReportUndefinedVariable(node.Name, eval.GetLocation(node).Span);
        }

        public void ReportUndefinedVariable(string name, SourceSpan span) {
            _diagnostics.Add(new DiagnosticsEntry(
                Resources.UndefinedVariable.FormatInvariant(name),
                span, ErrorCodes.UndefinedVariable, Severity.Warning, DiagnosticSource.Linter));
        }

        private void HandleGlobal(GlobalStatement node) {
            foreach (var nex in node.Names) {
                var m = Eval.LookupNameInScopes(nex.Name, out _, LookupOptions.Global);
                if (m == null) {
                    _diagnostics.Add(new DiagnosticsEntry(
                        Resources.ErrorVariableNotDefinedGlobally.FormatInvariant(nex.Name),
                        Eval.GetLocation(nex).Span, ErrorCodes.VariableNotDefinedGlobally, Severity.Warning, DiagnosticSource.Linter));
                }
            }
        }

        private void HandleNonLocal(NonlocalStatement node) {
            foreach (var nex in node.Names) {
                var m = Eval.LookupNameInScopes(nex.Name, out _, LookupOptions.Nonlocal);
                if (m == null) {
                    _diagnostics.Add(new DiagnosticsEntry(
                        Resources.ErrorVariableNotDefinedNonLocal.FormatInvariant(nex.Name),
                        Eval.GetLocation(nex).Span, ErrorCodes.VariableNotDefinedNonLocal, Severity.Warning, DiagnosticSource.Linter));
                }
            }
        }

        private void HandleScope(ScopeStatement node) {
            try {
                OpenScope(node);
                node.Walk(this);
            } finally {
                CloseScope();
            }
        }
    }
}
