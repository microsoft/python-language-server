using System.Collections.Generic;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Parsing {
    public class FStringBuilder {
        private readonly List<Expression> _children = new List<Expression>();

        public FStringBuilder() { }

        public FStringExpression Build() {
            return new FStringExpression(_children);
        }

        public void AppendString(string s) {
            _children.Add(new ConstantExpression(s));
        }

        public void Append(FStringExpression fStr) {
            _children.AddRange(fStr.Children);
        }

        public void AppendExpression(Expression subExpr) {
            _children.Add(subExpr);
        }
    }
}
