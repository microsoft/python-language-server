using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Parsing.Definition {
    public interface IScopeStatement: IScopeNode {
        Statement Body { get; }
    }
}
