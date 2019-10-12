using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Core.Text;

namespace Microsoft.Python.Parsing.Ast {
    public interface INode {
        int EndIndex { get; }

        int StartIndex { get; }

        IEnumerable<Node> GetChildNodes();
        void Walk(PythonWalker walker);
        Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default);

        string NodeName { get; }

        string ToCodeString(PythonAst ast);

        SourceLocation GetStart(PythonAst ast);

        SourceLocation GetEnd(PythonAst ast);

        SourceSpan GetSpan(PythonAst ast);

        /// <summary>
        /// Returns the proceeding whitespace (newlines and comments) that
        /// shows up before this node.
        /// </summary>
        string GetLeadingWhiteSpace(PythonAst ast);
    }
}
