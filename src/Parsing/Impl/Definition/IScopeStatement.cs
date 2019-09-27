using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Parsing {
    public interface IScopeStatement: IScopeNode {
        Statement Body { get; }
    }
}
