// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Collections.Generic;

namespace Microsoft.Python.Parsing.Ast {
    public abstract class ScopeExpression : Expression, IBindableNode {
        protected void Clear() => ScopeInfo.Clear();
        
        #region IScopeNode
        public IScopeNode ParentScopeNode { get; set; }

        public virtual Statement Body { get; }

        public virtual bool HasLateBoundVariableSets { get; set; }
        
        public bool IsClosure => ScopeInfo.IsClosure;

        IReadOnlyList<PythonVariable> IScopeNode.ScopeVariables => ScopeInfo.ScopeVariables;

        public bool ContainsNestedFreeVariables { get; set; }

        public bool NeedsLocalsDictionary { get; set; }

        public virtual string Name => "<unknown>";

        public bool TryGetVariable(string name, out PythonVariable variable) => ScopeInfo.TryGetVariable(name, out variable);

        public bool IsGlobal => ScopeInfo.IsGlobal;

        public PythonAst GlobalParent => ScopeInfo.GlobalParent;
        bool IScopeNode.ContainsNestedFreeVariables { get; set; }

        public IReadOnlyList<PythonVariable> FreeVariables => ScopeInfo.FreeVariables;
        
        #endregion
        
        internal abstract ScopeInfo ScopeInfo { get; }

        #region IBindableNode
        void IBindableNode.Bind(PythonNameBinder binder) => ScopeInfo.Bind(binder);

        void IBindableNode.FinishBind(PythonNameBinder binder) => ScopeInfo.FinishBind(binder);

        bool IBindableNode.TryBindOuter(IBindableNode from, string name, bool allowGlobals, out PythonVariable variable)
            => ScopeInfo.TryBindOuter(from, name, allowGlobals, out variable);
        
        void IBindableNode.AddVariable(PythonVariable variable) => ScopeInfo.AddVariable(variable);

        void IBindableNode.AddFreeVariable(PythonVariable variable, bool accessedInScope) => ScopeInfo.AddFreeVariable(variable, accessedInScope);

        string IBindableNode.AddReferencedGlobal(string name) => ScopeInfo.AddReferencedGlobal(name);

        void IBindableNode.AddNonLocalVariable(NameExpression name) => ScopeInfo.AddNonLocalVariable(name);

        void IBindableNode.AddCellVariable(PythonVariable variable) => ScopeInfo.AddCellVariable(variable);

        bool IBindableNode.ExposesLocalVariable(PythonVariable name) => ScopeInfo.ExposesLocalVariable(name);

        PythonVariable IBindableNode.BindReference(PythonNameBinder binder, string name) => ScopeInfo.BindReference(binder, name);

        PythonReference IBindableNode.Reference(string name) => ScopeInfo.Reference(name);

        bool IBindableNode.IsReferenced(string name) => ScopeInfo.IsReferenced(name);

        PythonVariable IBindableNode.CreateVariable(string name, VariableKind kind) => ScopeInfo.CreateVariable(name, kind);

        PythonVariable IBindableNode.EnsureVariable(string name) => ScopeInfo.EnsureVariable(name);
        
        PythonVariable IBindableNode.EnsureGlobalVariable(string name) => ScopeInfo.EnsureGlobalVariable(name);

        PythonVariable IBindableNode.DefineParameter(string name) => ScopeInfo.DefineParameter(name);

        bool IBindableNode.ContainsImportStar { get; set; }
        bool IBindableNode.ContainsExceptionHandling { get; set; }
        bool IBindableNode.ContainsUnqualifiedExec { get; set; }
        #endregion
    }
}
