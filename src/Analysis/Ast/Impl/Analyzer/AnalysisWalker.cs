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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    /// <summary>
    /// Base class with common functionality to module and function analysis walkers.
    /// </summary>
    internal abstract partial class AnalysisWalker : PythonWalkerAsync {
        private readonly HashSet<Node> _replacedByStubs = new HashSet<Node>();

        protected ExpressionLookup Lookup { get; }
        protected IServiceContainer Services => Lookup.Services;
        protected ILogger Log => Lookup.Log;
        protected IPythonModule Module => Lookup.Module;
        protected IPythonInterpreter Interpreter => Lookup.Interpreter;
        protected GlobalScope GlobalScope => Lookup.GlobalScope;
        protected PythonAst Ast => Lookup.Ast;
        protected AnalysisFunctionWalkerSet FunctionWalkers => Lookup.FunctionWalkers;

        protected AnalysisWalker(ExpressionLookup lookup) {
            Lookup = lookup;
        }
        protected AnalysisWalker(IServiceContainer services, IPythonModule module, PythonAst ast) {
            Lookup = new ExpressionLookup(services, module, ast);
        }

        public virtual async Task<IGlobalScope> CompleteAsync(CancellationToken cancellationToken = default) {
            await FunctionWalkers.ProcessSetAsync(cancellationToken);
            _replacedByStubs.Clear();
            return GlobalScope;
        }

        internal LocationInfo GetLoc(ClassDefinition node) {
            if (node == null || node.StartIndex >= node.EndIndex) {
                return null;
            }

            var start = node.NameExpression?.GetStart(Ast) ?? node.GetStart(Ast);
            var end = node.GetEnd(Ast);
            return new LocationInfo(Module.FilePath, Module.Uri, start.Line, start.Column, end.Line, end.Column);
        }

        private LocationInfo GetLoc(Node node) => Lookup.GetLoc(node);

        protected static string GetDoc(SuiteStatement node) {
            var docExpr = node?.Statements?.FirstOrDefault() as ExpressionStatement;
            var ce = docExpr?.Expression as ConstantExpression;
            return ce?.Value as string;
        }

        protected IMember GetMemberFromStub(string name) {
            if (Module.Stub == null) {
                return null;
            }

            var memberNameChain = new List<string>(Enumerable.Repeat(name, 1));
            IScope scope = Lookup.CurrentScope;

            while (scope != GlobalScope) {
                memberNameChain.Add(scope.Name);
                scope = scope.OuterScope;
            }

            IMember member = Module.Stub;
            for (var i = memberNameChain.Count - 1; i >= 0; i--) {
                if (!(member is IMemberContainer mc)) {
                    return null;
                }
                member = mc.GetMember(memberNameChain[i]);
                if (member == null) {
                    return null;
                }
            }

            return member;
        }
    }
}
