using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Python.Parsing.Ast {
    public class FStringExpression : Expression {
        private readonly List<Expression> _children;
        private readonly string _fString;

        public FStringExpression(List<Expression> children, string fString) {
            _children = children;
            _fString = fString;
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
        }

        public override Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            throw new NotImplementedException();
        }
    }
}
