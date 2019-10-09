using System.Collections.Generic;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Parsing {
    internal interface IBindableNode : IScopeNode {
        void Bind(PythonNameBinder binder);

        void FinishBind(PythonNameBinder binder);

        bool TryBindOuter(IBindableNode from, string name, bool allowGlobals, out PythonVariable variable);

        void AddFreeVariable(PythonVariable variable, bool accessedInScope);

        string AddReferencedGlobal(string name);

        void AddNonLocalVariable(NameExpression name);

        void AddCellVariable(PythonVariable variable);

        bool ExposesLocalVariable(PythonVariable name);

        PythonVariable BindReference(PythonNameBinder binder, string name);

        void AddVariable(PythonVariable variable);

        PythonReference Reference(string name);

        bool IsReferenced(string name);

        PythonVariable CreateVariable(string name, VariableKind kind);

        PythonVariable EnsureVariable(string name);

        PythonVariable EnsureGlobalVariable(string name);

        PythonVariable DefineParameter(string name);
        
        
        bool ContainsImportStar { get; set; }
        bool ContainsExceptionHandling { get; set; }

        bool ContainsUnqualifiedExec { get; set; }
        
        /// <summary>
        /// True if variables can be set in a late bound fashion that we don't
        /// know about at code gen time - for example via from fob import *.
        /// 
        /// This is tracked independently of the ContainsUnqualifiedExec/NeedsLocalsDictionary
        /// </summary>
        bool HasLateBoundVariableSets { get; set; }

        Dictionary<string, PythonVariable> Variables { get; set; }
    }
}
