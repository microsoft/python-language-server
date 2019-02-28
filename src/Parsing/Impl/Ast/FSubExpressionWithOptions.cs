using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Python.Parsing.Ast {
    public class FSubExpressionWithOptions : Expression {
        private Expression _expr;
        private Expression _formatExpression;
        private Expression _conversionExpression;

        public FSubExpressionWithOptions(Expression expr, Expression formatExpression, Expression conversionExpression) {
            _expr = expr;
            _formatExpression = formatExpression;
            _conversionExpression = conversionExpression;
        }

        public override IEnumerable<Node> GetChildNodes() {
            return new List<Node>() { _expr, _formatExpression, _conversionExpression };
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                _expr.Walk(walker);
                _formatExpression?.Walk(walker);
                _conversionExpression?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        public override Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            throw new NotImplementedException();
        }
    }
}
