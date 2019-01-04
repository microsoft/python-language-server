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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Analyzer.Evaluation;
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
        public ExpressionEval Eval { get; }
        public IServiceContainer Services => Eval.Services;
        public ILogger Log => Eval.Log;
        public IPythonModule Module => Eval.Module;
        public IPythonInterpreter Interpreter => Eval.Interpreter;
        public GlobalScope GlobalScope => Eval.GlobalScope;
        public PythonAst Ast => Eval.Ast;
        protected MemberWalkerSet MemberWalkers => Eval.MemberWalkers;
        protected HashSet<Node> ReplacedByStubs { get; } = new HashSet<Node>();

        protected AnalysisWalker(ExpressionEval eval) {
            Eval = eval;
        }
        protected AnalysisWalker(IServiceContainer services, IPythonModule module, PythonAst ast) {
            Eval = new ExpressionEval(services, module, ast);
        }

        internal LocationInfo GetLoc(ClassDefinition node) {
            if (node == null || node.StartIndex >= node.EndIndex) {
                return null;
            }

            var start = node.NameExpression?.GetStart(Ast) ?? node.GetStart(Ast);
            var end = node.GetEnd(Ast);
            return new LocationInfo(Module.FilePath, Module.Uri, start.Line, start.Column, end.Line, end.Column);
        }

        private LocationInfo GetLoc(Node node) => Eval.GetLoc(node);

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
            IScope scope = Eval.CurrentScope;

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

        protected PythonClassType CreateClass(ClassDefinition node) {
            node = node ?? throw new ArgumentNullException(nameof(node));
            return new PythonClassType(
                node,
                Module,
                GetDoc(node.Body as SuiteStatement),
                GetLoc(node),
                Interpreter,
                Eval.SuppressBuiltinLookup ? BuiltinTypeId.Unknown : BuiltinTypeId.Type); // built-ins set type later
        }

        protected T[] GetStatements<T>(ScopeStatement s)
            => (s.Body as SuiteStatement)?.Statements.OfType<T>().ToArray() ?? Array.Empty<T>();
    }
}
