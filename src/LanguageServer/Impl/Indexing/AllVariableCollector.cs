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
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Indexing {
    /// <summary>
    /// This is a poor man's __all__ values collector. it uses only syntactic information to gather values.
    /// 
    /// unlike the real one <see cref="Analysis.Analyzer.ModuleWalker" /> that actually binds expressions and 
    /// uses semantic data to build up __all__ information, this one's purpose is gathering cheap and fast but might be incorrect data 
    /// until more expensive analysis is done.
    /// </summary>
    internal class AllVariableCollector : PythonWalker {
        private const string AllVariableName = "__all__";
        private readonly CancellationToken _cancellationToken;

        /// <summary>
        /// names assigned to __all__
        /// </summary>
        public readonly HashSet<string> Names;

        public AllVariableCollector(CancellationToken cancellationToken) {
            _cancellationToken = cancellationToken;
            Names = new HashSet<string>();
        }

        public override bool Walk(AssignmentStatement node) {
            _cancellationToken.ThrowIfCancellationRequested();

            // make sure we are dealing with __all__ assignment
            if (node.Left.OfType<NameExpression>().Any(n => n.Name == AllVariableName)) {
                AddNames(node.Right as ListExpression);
            }

            return base.Walk(node);
        }

        public override bool Walk(AugmentedAssignStatement node) {
            _cancellationToken.ThrowIfCancellationRequested();

            if (node.Operator == Parsing.PythonOperator.Add &&
                node.Left is NameExpression nex &&
                nex.Name == AllVariableName) {
                AddNames(node.Right as ListExpression);
            }

            return base.Walk(node);
        }

        public override bool Walk(CallExpression node) {
            _cancellationToken.ThrowIfCancellationRequested();

            if (node.Args.Count > 0 &&
                node.Target is MemberExpression me &&
                me.Target is NameExpression nex &&
                nex.Name == AllVariableName) {
                var arg = node.Args[0].Expression;

                switch (me.Name) {
                    case "append":
                        AddName(arg);
                        break;
                    case "extend":
                        AddNames(arg as ListExpression);
                        break;
                }
            }

            return base.Walk(node);
        }

        private void AddName(Expression item) {
            if (item is ConstantExpression con &&
                con.Value is string name &&
                !string.IsNullOrEmpty(name)) {
                Names.Add(name);
            }
        }

        private void AddNames(ListExpression list) {
            // only support the form of __all__ = [ ... ]
            if (list == null) {
                return;
            }

            foreach (var item in list.Items) {
                AddName(item);
            }
        }
    }
}
