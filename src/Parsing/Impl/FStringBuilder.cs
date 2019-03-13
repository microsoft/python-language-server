using System.Collections.Generic;
using System.Text;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Parsing {
    public interface IFStringBuilder {
        Expression Build();
        void Append(string s, bool isRaw);
        void Append(ConstantExpression expr);
        void Append(FString fStr);
        void Append(Node subExpr);
    }

    public abstract class FStringBuilder : IFStringBuilder {
        protected readonly List<Node> _children = new List<Node>();

        public abstract Expression Build();

        public void Append(string s, bool isRaw) {
            string contents = "";
            try {
                contents = LiteralParser.ParseString(s.ToCharArray(),
                0, s.Length, isRaw, isUni: true, normalizeLineEndings: true, allowTrailingBackslash: true);
            } catch (DecoderFallbackException e) {
                throw e;
            } finally {
                _children.Add(new ConstantExpression(contents));
            }
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

    public class FormatSpecifierBuilder : FStringBuilder {
        public override Expression Build() {
            return new FormatSpecifier(_children.ToArray());
        }
    }

    public class RootFStringBuilder : FStringBuilder {
        private readonly string _openQuotes;

        public RootFStringBuilder(string openQuotes) {
            _openQuotes = openQuotes;
        }

        public override Expression Build() {
            return new FString(_children.ToArray(), _openQuotes);
        }
    }
}
