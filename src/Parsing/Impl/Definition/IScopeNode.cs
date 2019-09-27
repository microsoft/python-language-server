using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Parsing {
    /// <summary>
    /// Represents a <see cref="Node"/> with a Scope
    /// </summary>
    public interface IScopeNode : INode {
        /// <summary>
        /// Scope node name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Parent of the current scope
        /// </summary>
        IScopeNode Parent { get; set; }

        /// <summary>
        /// Body of the scope node, empty statement if not applicable 
        /// </summary>
        Statement Body { get; }

        /// <summary>
        /// Gloabl scope
        /// </summary>
        PythonAst GlobalParent { get; }

        /// <summary>
        /// Holds information about the scope gathered from Parser, mainly used in  <see cref="PythonNameBinder"/>
        /// </summary>
        ScopeInfo ScopeInfo { get; }

        /// <summary>
        /// Binds the current scope, giving <param name="binder">binder</param> access to stored variables in <see cref="ScopeInfo"/>
        /// </summary>
        void Bind(PythonNameBinder binder);

        /// <summary>
        /// Completes the binding of the current scope
        /// </summary>
        void FinishBind(PythonNameBinder binder);

        /// <summary>
        /// Accesses variables in scope
        /// </summary>
        bool TryGetVariable(string name, out PythonVariable variable);
    }
}
