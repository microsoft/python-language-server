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
        IScopeNode ParentNode { get; set; }

        /// <summary>
        /// Body of the scope node, null if not applicable 
        /// </summary>
        Statement Body { get; }

        /// <summary>
        /// Gloabl scope
        /// </summary>
        PythonAst GlobalParent { get; }
        bool ContainsNestedFreeVariables { get; set; }
        
        bool NeedsLocalsDictionary { get; set; }
        
        bool IsClosure { get; }
        
        IReadOnlyList<PythonVariable> ScopeVariables { get; }
        
        IReadOnlyList<PythonVariable> FreeVariables { get; }

        bool TryGetVariable(string name, out PythonVariable variable);
        
        bool IsGlobal { get; }
    }
}
