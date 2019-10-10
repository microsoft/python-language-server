using Microsoft.Python.Parsing;

namespace Microsoft.Python.Parsing.Ast {
    internal class AstScopeDelegate : ScopeDelegate {
        public AstScopeDelegate(IBindableNode node) : base(node) { }

        internal override bool IsGlobal => true;

        internal override bool ExposesLocalVariable(PythonVariable name) => true;

        internal override PythonVariable BindReference(PythonNameBinder binder, string name) => EnsureVariable(name);

        internal override bool TryBindOuter(IBindableNode from, string name, bool allowGlobals, out PythonVariable variable) {
            if (allowGlobals) {
                // Unbound variable
                from.AddReferencedGlobal(name);

                if (from.HasLateBoundVariableSets) {
                    // If the context contains unqualified exec, new locals can be introduced
                    // Therefore we need to turn this into a fully late-bound lookup which
                    // happens when we don't have a PythonVariable.
                    variable = null;
                    return false;
                }
                
                // Create a global variable to bind to.
                variable = EnsureGlobalVariable(name);
                return true;
            }

            variable = null;
            return false;
        }
    }
}
