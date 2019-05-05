using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Python.Parsing.Ast {
    public class NamedExpression : Expression {
        private const string _nodeName = "named expression";

        public NamedExpression(Expression lhs, Expression rhs) {
            Target = lhs;
            Value = rhs;
        }

        public Expression Target { get; }
        public Expression Value { get; }

        public override string NodeName => _nodeName;

        public override IEnumerable<Node> GetChildNodes() {
            yield return Target;
            if (Value != null) yield return Value;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Target.Walk(walker);
                Value.Walk(walker);
            }
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                await Target.WalkAsync(walker, cancellationToken);
                await Value.WalkAsync(walker, cancellationToken);
            }
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            Target.AppendCodeString(res, ast, format);
            res.Append(" := ");
            Value.AppendCodeString(res, ast, format);
        }
    }
}
