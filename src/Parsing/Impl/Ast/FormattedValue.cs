using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Python.Parsing.Ast {
    public class FormattedValue : Expression {
        private readonly Expression _expr;
        private readonly Expression _formatExpression;
        private readonly char? _conversionExpression;

        public FormattedValue(Expression expr, char? conversion, Expression formatExpression) {
            _expr = expr;
            _formatExpression = formatExpression;
            _conversionExpression = conversion;
        }

        public override IEnumerable<Node> GetChildNodes() {
            // _conversionExpression ???
            return new[] { _expr, _formatExpression };
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                _expr.Walk(walker);
                _formatExpression?.Walk(walker);
                //_conversionExpression?.Walk(walker);
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
