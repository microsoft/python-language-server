using System.Collections.Generic;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Parsing {
    public class FStringBuilder {
        private readonly List<Node> _children = new List<Node>();

        public FStringBuilder() { }

        public FStringExpression Build() {
            return new FStringExpression(_children);
        }

        public void AppendString(string s) {
            _children.Add(new ConstantExpression(s));
        }

        public void Append(FStringExpression fStr) {
            _children.AddRange(fStr.GetChildNodes());
        }

        public void AppendExpression(Expression subExpr) {
            _children.Add(subExpr);
        }
    }
}
