using Microsoft.Python.Parsing;

namespace Microsoft.Python.Parsing.Ast {
    internal class ClassScopeDelegate : ScopeDelegate {
        private readonly ClassDefinition _classDefinition;
        
        public ClassScopeDelegate(ClassDefinition node) : base(node) {
            _classDefinition = node;
        }

       
        
        internal override bool ExposesLocalVariable(PythonVariable name) => true;

        internal override bool TryBindOuter(IBindableNode from, string name, bool allowGlobals,
                                            out PythonVariable variable) {
            if (name == "__class__" && _classDefinition.ClassVariable != null) {
                // 3.x has a cell var called __class__ which can be bound by inner scopes
                variable = _classDefinition.ClassVariable;
                return true;
            }

            return base.TryBindOuter(from, name, allowGlobals, out variable);
        }

        internal override PythonVariable BindReference(PythonNameBinder binder, string name) {
            // Python semantics: The variables bound local in the class
            // scope are accessed by name - the dictionary behavior of classes
            if (TryGetVariable(name, out var variable)) {
                // TODO: This results in doing a dictionary lookup to get/set the local,
                // when it should probably be an uninitialized check / global lookup for gets
                // and a direct set
                if (variable.Kind == VariableKind.Global) {
                    AddReferencedGlobal(name);
                } else if (variable.Kind == VariableKind.Local) {
                    return null;
                }

                return variable;
            }

            // Try to bind in outer scopes, if we have an unqualified exec we need to leave the
            // variables as free for the same reason that locals are accessed by name.
            for (var parent = Node.ParentNode; parent != null; parent = parent.ParentNode) {
                if ((parent as IBindableNode)?.TryBindOuter(_classDefinition, name, true, out variable) ?? false) {
                    return variable;
                }
            }

            return null;
        }
    }
}
