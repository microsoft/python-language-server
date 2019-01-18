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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Core;

namespace Microsoft.Python.Parsing.Ast {
    public sealed class GeneratorExpression : Comprehension {
        private readonly ComprehensionIterator[] _iterators;

        public GeneratorExpression(Expression item, ComprehensionIterator[] iterators) {
            Item = item;
            _iterators = iterators;
        }

        public override IList<ComprehensionIterator> Iterators => _iterators;

        public override string NodeName => "generator";

        public Expression Item { get; }

        internal override string CheckAssign() => "can't assign to generator expression";

        internal override string CheckAugmentedAssign() => CheckAssign();

        internal override string CheckDelete() => "can't delete generator expression";

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Item?.Walk(walker);
                foreach (var ci in _iterators.MaybeEnumerate()) {
                    ci.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                if (Item != null) {
                    await Item.WalkAsync(walker, cancellationToken);
                }
                foreach (var ci in _iterators.MaybeEnumerate()) {
                    await ci.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            if (this.IsAltForm(ast)) {
                AppendCodeString(res, ast, format, string.Empty, string.Empty, Item);
            } else {
                AppendCodeString(res, ast, format, "(", this.IsMissingCloseGrouping(ast) ? string.Empty : ")", Item);
            }
        }
    }
}
