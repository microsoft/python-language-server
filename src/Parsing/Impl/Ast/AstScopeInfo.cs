using Microsoft.Python.Parsing;

namespace Microsoft.Python.Parsing.Ast {
    public class AstScopeInfo : ScopeInfo {
        public AstScopeInfo(IScopeNode node) : base(node) { }

        internal override bool IsGlobal => true;

        protected override bool ExposesLocalVariable => true;

        internal override PythonVariable BindReference(PythonNameBinder binder, string name) => EnsureVariable(name);

        internal override bool TryBindOuter(IScopeNode from, string name, bool allowGlobals, out PythonVariable variable) {
            if (allowGlobals) {
                // Unbound variable
                from.ScopeInfo.AddReferencedGlobal(name);

                if (from.ScopeInfo.HasLateBoundVariableSets) {
                    // If the context contains unqualified exec, new locals can be introduced
                    // Therefore we need to turn this into a fully late-bound lookup which
                    // happens when we don't have a PythonVariable.
                    variable = null;
                    return false;
                } else {
                    // Create a global variable to bind to.
                    variable = EnsureGlobalVariable(name);
                    return true;
                }
            }

            variable = null;
            return false;
        }
    }
}
