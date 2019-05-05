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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Python.Parsing.Ast {
    public class NameExpression : Expression {
        private const string _nodeName = "name";

        public NameExpression(string name) {
            Name = name ?? "";
        }

        public string/*!*/ Name { get; }

        public override string ToString() => $"{base.ToString()}:{Name}";
        internal override string CheckAssign() => null;
        internal override string CheckDelete() => null;
        internal override string CheckAssignExpr() => null;
        public override string NodeName => _nodeName;

        public override IEnumerable<Node> GetChildNodes() => Enumerable.Empty<Node>();

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
            }
            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            format.ReflowComment(res, this.GetPreceedingWhiteSpaceDefaultNull(ast));
            if (format.UseVerbatimImage) {
                res.Append(this.GetVerbatimImage(ast) ?? Name);
            } else {
                res.Append(Name);
            }
        }
    }
}
