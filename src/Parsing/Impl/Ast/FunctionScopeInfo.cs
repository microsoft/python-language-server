using Microsoft.Python.Core;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.Parsing.Ast {
    public class FunctionScopeInfo : ScopeInfo {
        public FunctionScopeInfo(IScopeNode node) : base(node) { }

        protected override bool ExposesLocalVariable => NeedsLocalsDictionary;

        internal override bool TryBindOuter(IScopeNode from, string name, bool allowGlobals,
                                            out PythonVariable variable) {
            // Functions expose their locals to direct access
            ContainsNestedFreeVariables = true;
            if (TryGetVariable(name, out variable)) {
                variable.AccessedInNestedScope = true;

                if (variable.Kind == VariableKind.Local || variable.Kind == VariableKind.Parameter) {
                    from.ScopeInfo.AddFreeVariable(variable, true);

                    for (var scope = from.ParentNode; scope != Node; scope = scope.ParentNode) {
                        scope.ScopeInfo.AddFreeVariable(variable, false);
                    }

                    AddCellVariable(variable);
                } else if (allowGlobals) {
                    from.ScopeInfo.AddReferencedGlobal(name);
                }

                return true;
            }

            return false;
        }

        internal override PythonVariable BindReference(PythonNameBinder binder, string name) {
            // First try variables local to this scope
            if (TryGetVariable(name, out var variable) && variable.Kind != VariableKind.Nonlocal) {
                if (variable.Kind == VariableKind.Global) {
                    AddReferencedGlobal(name);
                }

                return variable;
            }

            // Try to bind in outer scopes
            for (var parent = Node.ParentNode; parent != null; parent = parent.ParentNode) {
                if (parent.ScopeInfo.TryBindOuter(Node, name, true, out variable)) {
                    return variable;
                }
            }

            return null;
        }

        internal override void Bind(PythonNameBinder binder) {
            base.Bind(binder);
            Verify(binder);
        }

        private void Verify(PythonNameBinder binder) {
            if (ContainsImportStar && IsClosure) {
                binder.ReportSyntaxError(
                                         "import * is not allowed in function '{0}' because it is a nested function"
                                            .FormatUI(Name),
                                         Node);
            }

            if (ContainsImportStar && Node.ParentNode is FunctionDefinition) {
                binder.ReportSyntaxError(
                                         "import * is not allowed in function '{0}' because it is a nested function"
                                            .FormatUI(Name),
                                         Node);
            }

            if (ContainsImportStar && ContainsNestedFreeVariables) {
                binder.ReportSyntaxError(
                                         "import * is not allowed in function '{0}' because it contains a nested function with free variables"
                                            .FormatUI(Name),
                                         Node);
            }

            if (ContainsUnqualifiedExec && ContainsNestedFreeVariables) {
                binder.ReportSyntaxError(
                                         "unqualified exec is not allowed in function '{0}' because it contains a nested function with free variables"
                                            .FormatUI(Name),
                                         Node);
            }

            if (ContainsUnqualifiedExec && IsClosure) {
                binder.ReportSyntaxError(
                                         "unqualified exec is not allowed in function '{0}' because it is a nested function"
                                            .FormatUI(Name),
                                         Node);
            }
        }
    }
}
