using Microsoft.Python.Core;

namespace Microsoft.Python.Parsing.Ast {
    internal class FunctionScopeDelegate : ScopeDelegate {
        public FunctionScopeDelegate(IBindableNode node) : base(node) { }

        internal override bool ExposesLocalVariable(PythonVariable name) => Node.NeedsLocalsDictionary;

        internal override bool TryBindOuter(IBindableNode from, string name, bool allowGlobals,
                                            out PythonVariable variable) {
            // Functions expose their locals to direct access
            Node.ContainsNestedFreeVariables = true;
            if (TryGetVariable(name, out variable)) {
                variable.AccessedInNestedScope = true;

                var boundScope = from;
                if (variable.Kind == VariableKind.Local || variable.Kind == VariableKind.Parameter) {
                    boundScope?.AddFreeVariable(variable, true);

                    for (var scope = from.ParentScopeNode; scope != Node; scope = scope.ParentScopeNode) {
                        (scope as IBindableNode)?.AddFreeVariable(variable, false);
                    }

                    AddCellVariable(variable);
                } else if (allowGlobals) {
                    boundScope?.AddReferencedGlobal(name);
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
            for (var parent = Node.ParentScopeNode; parent != null; parent = parent.ParentScopeNode) {
                if ((parent as IBindableNode)?.TryBindOuter(Node, name, true, out variable) ?? false) {
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
            if (Node.ContainsImportStar && IsClosure) {
                binder.ReportSyntaxError(
                                         "import * is not allowed in function '{0}' because it is a nested function"
                                            .FormatUI(Node.Name),
                                         Node);
            }

            if (Node.ContainsImportStar && Node.ParentScopeNode is FunctionDefinition) {
                binder.ReportSyntaxError(
                                         "import * is not allowed in function '{0}' because it is a nested function"
                                            .FormatUI(Node.Name),
                                         Node);
            }

            if (Node.ContainsImportStar && Node.ContainsNestedFreeVariables) {
                binder.ReportSyntaxError(
                                         "import * is not allowed in function '{0}' because it contains a nested function with free variables"
                                            .FormatUI(Node.Name),
                                         Node);
            }

            if (Node.ContainsUnqualifiedExec && Node.ContainsNestedFreeVariables) {
                binder.ReportSyntaxError(
                                         "unqualified exec is not allowed in function '{0}' because it contains a nested function with free variables"
                                            .FormatUI(Node.Name),
                                         Node);
            }

            if (Node.ContainsUnqualifiedExec && IsClosure) {
                binder.ReportSyntaxError(
                                         "unqualified exec is not allowed in function '{0}' because it is a nested function"
                                            .FormatUI(Node.Name),
                                         Node);
            }
        }
    }
}
