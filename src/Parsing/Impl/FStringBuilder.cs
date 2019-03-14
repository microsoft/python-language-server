using System.Collections.Generic;
using System.Text;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Parsing {
    public interface IFStringBuilder {
        Expression Build();
        void Append(string s);
        void Append(AsciiString s);
        void Append(FString fStr);
        void Append(Node subExpr);
        void AddUnparsedFString(string s);
    }

    public abstract class FStringBuilder : IFStringBuilder {
        protected readonly List<Node> _children = new List<Node>();
        protected readonly StringBuilder _unparsedFStringBuilder = new StringBuilder();

        public abstract Expression Build();

        public void Append(string s) {
            _unparsedFStringBuilder.Append(s);
            _children.Add(new ConstantExpression(s));
        }

        public void Append(AsciiString s) {
            _unparsedFStringBuilder.Append(s.String);
            _children.Add(new ConstantExpression(s));
        }

        public void Append(FString fStr) {
            _unparsedFStringBuilder.Append(fStr.Unparsed);
            _children.AddRange(fStr.GetChildNodes());
        }

        public void Append(Node subExpr) {
            _children.Add(subExpr);
        }

        public void AddUnparsedFString(string s) {
            _unparsedFStringBuilder.Append(s);
        }
    }

    public class FormatSpecifierBuilder : FStringBuilder {
        public override Expression Build() {
            return new FormatSpecifier(_children.ToArray(), _unparsedFStringBuilder.ToString());
        }
    }

    public class RootFStringBuilder : FStringBuilder {
        private readonly string _openQuotes;

        public RootFStringBuilder(string openQuotes) {
            _openQuotes = openQuotes;
        }

        public override Expression Build() {
            return new FString(_children.ToArray(), _openQuotes, _unparsedFStringBuilder.ToString());
        }
    }
}
