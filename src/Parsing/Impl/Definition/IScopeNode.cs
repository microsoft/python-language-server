using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Parsing.Definition {
    public interface IScopeNode : INode {
        string ScopeName { get; }
        IScopeNode Parent { get; set;  }
        ScopeInfo ScopeInfo { get; }
        void Bind(PythonNameBinder binder);
        void FinishBind(PythonNameBinder binder);

        bool TryGetVariable(string name, out PythonVariable variable);
    }
}
