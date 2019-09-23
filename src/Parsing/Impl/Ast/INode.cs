using Microsoft.Python.Core.Text;

namespace Microsoft.Python.Parsing.Ast {
    public interface INode {
        int StartIndex { get; }
        int EndIndex { get; }
        void Walk(PythonWalker walker);

        SourceLocation GetStart(PythonAst ast);

        SourceLocation GetEnd(PythonAst ast);

        SourceSpan GetSpan(PythonAst ast);
    }
}
