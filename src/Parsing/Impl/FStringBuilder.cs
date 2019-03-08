using System.Collections.Generic;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Parsing {
    public class FStringBuilder {
        private readonly List<Node> _children = new List<Node>();

        public FStringBuilder() { }

        public FString Build() {
            return new FString(_children);
        }

        public void Append(string s, bool isRaw) {
            if (isRaw) {
                System.Console.WriteLine("");
            }
            _children.Add(new ConstantExpression(LiteralParser.ParseString(s.ToCharArray(), 
                0, s.Length, isRaw, isUni:true, normalizeLineEndings:true)));
        }

        public void Append(ConstantExpression expr) {
            _children.Add(expr);
        }

        public void Append(FString fStr) {
            _children.AddRange(fStr.GetChildNodes());
        }

        public void Append(Node subExpr) {
            _children.Add(subExpr);
        }
    }
}
