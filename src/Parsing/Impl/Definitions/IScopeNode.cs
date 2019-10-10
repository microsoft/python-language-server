using System.Collections.Generic;
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
        IScopeNode ParentScopeNode { get; set; }

        /// <summary>
        /// Body of the scope node, null if not applicable 
        /// </summary>
        Statement Body { get; }

        /// <summary>
        /// Global scope
        /// </summary>
        PythonAst GlobalParent { get; }

        IReadOnlyList<PythonVariable> ScopeVariables { get; }

        IReadOnlyList<PythonVariable> FreeVariables { get; }

        bool ContainsNestedFreeVariables { get; set; }

        bool NeedsLocalsDictionary { get; set; }

        bool IsClosure { get; }

        bool TryGetVariable(string name, out PythonVariable variable);

        bool IsGlobal { get; }
    }
}
