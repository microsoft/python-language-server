using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Python.Parsing.Ast {
    public class FormattedValue : Expression {
        public FormattedValue(Expression value, char? conversion, Expression formatSpecifier) {
            Value = value;
            FormatSpecifier = formatSpecifier;
            Conversion = conversion;
        }

        public Expression Value { get; }
        public Expression FormatSpecifier { get; }
        public char? Conversion { get; }

        public override IEnumerable<Node> GetChildNodes() {
            return new[] { Value, FormatSpecifier };
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Value.Walk(walker);
                FormatSpecifier?.Walk(walker);
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
