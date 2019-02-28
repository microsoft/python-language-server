using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Parsing {
    public class FStringBuilder {
        private readonly StringBuilder completeFString = new StringBuilder();
        private readonly List<Expression> _children = new List<Expression>();

        public FStringBuilder() { }

        public FStringExpression Build() {
            return new FStringExpression(_children, completeFString.ToString());
        }

        public void AppendString(string s) {
            completeFString.Append(s);
            _children.Add(new ConstantExpression(s));
        }

        public void Append(FStringExpression fStr) {
            completeFString.Append(fStr.String);
            _children.AddRange(fStr.Children);
        }

        public void AppendExpression(string subExprStr) {
            completeFString.Append(subExprStr);
            _children.Add(CreateExpression(subExprStr));
        }

        private Expression CreateExpression(string subExprStr) {
            Check.ArgumentNotNullOrEmpty(nameof(subExprStr), subExprStr);

            var parser = Parser.CreateParser(new StringReader(subExprStr), PythonLanguageVersion.V36);
            var expr = Statement.GetExpression(parser.ParseTopExpression().Body);
            if (expr is null) {
                throw new Exception("Expression failed to parse");
            }
            return expr;
        }
    }
}
