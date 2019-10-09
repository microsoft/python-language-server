using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Core.Text;

namespace Microsoft.Python.Parsing.Ast {
    public interface INode {
        int EndIndex { get; set; }

        int StartIndex { get; set; }

        IEnumerable<Node> GetChildNodes();
        void Walk(PythonWalker walker);
        Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default);

        string NodeName { get; }

        string ToCodeString(PythonAst ast);

        string ToCodeString(PythonAst ast, CodeFormattingOptions format);

        SourceLocation GetStart(PythonAst ast);

        SourceLocation GetEnd(PythonAst ast);

        SourceSpan GetSpan(PythonAst ast);

        /// <summary>
        /// Returns the proceeding whitespace (newlines and comments) that
        /// shows up before this node.
        /// 
        /// New in 1.1.
        /// </summary>
        string GetLeadingWhiteSpace(PythonAst ast);

        /// <summary>
        /// Sets the proceeding whitespace (newlines and comments) that shows up
        /// before this node.
        /// </summary>
        /// <param name="ast"></param>
        /// <param name="whiteSpace"></param>
        void SetLeadingWhiteSpace(PythonAst ast, string whiteSpace);
    }
}
