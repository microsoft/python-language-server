using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Python.Parsing.Ast {
    public class FString : Expression {
        private readonly IEnumerable<Node> _children;

        public FString(IEnumerable<Node> children) {
            _children = children;
        }

        public override IEnumerable<Node> GetChildNodes() {
            return _children;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                foreach (var child in _children) {
                    child.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        public override Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            var verbatimPieces = this.GetVerbatimNames(ast);
            var verbatimComments = this.GetListWhiteSpace(ast);
            if (verbatimPieces != null) {
                // string+ / bytes+, such as "abc" "abc", which can spawn multiple lines, and 
                // have comments in between the peices.
                for (var i = 0; i < verbatimPieces.Length; i++) {
                    if (verbatimComments != null && i < verbatimComments.Length) {
                        format.ReflowComment(res, verbatimComments[i]);
                    }
                    res.Append(verbatimPieces[i]);
                }
            } else {
                format.ReflowComment(res, this.GetPreceedingWhiteSpaceDefaultNull(ast));
                res.Append(this.GetExtraVerbatimText(ast));
            }
        }
    }
}
