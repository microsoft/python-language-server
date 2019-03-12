using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Microsoft.Python.Core;

namespace Microsoft.Python.Parsing.Ast {
    public class FormatSpecifer : FString {
        public FormatSpecifer(IEnumerable<Node> children, string openQuotes = "") : base(children, openQuotes) {
            // No quotes
            Debug.Assert(openQuotes.IsNullOrEmpty());
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            // There is no leading f
            foreach (var child in _children) {
                AppendChild(res, ast, format, child);
            }
        }
    }
}
