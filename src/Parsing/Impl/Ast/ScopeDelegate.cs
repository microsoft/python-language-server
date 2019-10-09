using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Core.Diagnostics;

namespace Microsoft.Python.Parsing.Ast {
    internal abstract class ScopeDelegate {
        // Storing variables due to "exec" or call to dir, locals, eval, vars...

        // list of variables accessed from outer scopes
        private List<PythonVariable> _freeVars;

        // global variables accessed from this scope
        private List<string> _globalVars;

        // variables accessed from nested scopes
        private List<string> _cellVars;

        // variables declared as nonlocal within this scope
        private List<NameExpression> _nonLocalVars;

        // names of all variables referenced, null after binding completes
        private Dictionary<string, List<PythonReference>> _references;

        protected ScopeDelegate(IBindableNode node) {
            Check.ArgumentNotNull(nameof(node), node);
            Node = node;
        }

        protected IBindableNode Node { get; }

        /// <summary>
        /// True if this scope accesses a variable from an outer scope.
        /// </summary>
        public bool IsClosure => FreeVariables != null && FreeVariables.Count > 0;

        internal Dictionary<string, PythonVariable> Variables { get; private set; }

        /// <summary>
        /// Gets the variables for this scope.
        /// </summary>
        public IReadOnlyList<PythonVariable> ScopeVariables => Variables?.Values.ToImmutableArray() ?? ImmutableArray<PythonVariable>.Empty;

        internal virtual bool IsGlobal => false;

        public PythonAst GlobalParent {
            get {
                var cur = Node as IScopeNode;
                while (!(cur is PythonAst)) {
                    Debug.Assert(cur != null);
                    cur = cur.ParentNode;
                }

                return (PythonAst) cur;
            }
        }

        internal void Clear() {
            _references?.Clear();
            _cellVars?.Clear();
            _freeVars?.Clear();
            _globalVars?.Clear();
            _nonLocalVars?.Clear();
        }

        internal void AddFreeVariable(PythonVariable variable, bool accessedInScope) {
            _freeVars = _freeVars ?? new List<PythonVariable>();
            if (!_freeVars.Contains(variable)) {
                _freeVars.Add(variable);
            }
        }

        internal string AddReferencedGlobal(string name) {
            _globalVars = _globalVars ?? new List<string>();
            if (!_globalVars.Contains(name)) {
                _globalVars.Add(name);
            }

            return name;
        }

        internal void AddNonLocalVariable(NameExpression name) {
            _nonLocalVars = _nonLocalVars ?? new List<NameExpression>();
            _nonLocalVars.Add(name);
        }

        internal void AddCellVariable(PythonVariable variable) {
            _cellVars = _cellVars ?? new List<string>();
            if (!_cellVars.Contains(variable.Name)) {
                _cellVars.Add(variable.Name);
            }
        }

        /// <summary>
        /// Variables that are bound in an outer scope - but not a global scope
        /// </summary>
        public IReadOnlyList<PythonVariable> FreeVariables => _freeVars;

        internal abstract bool ExposesLocalVariable(PythonVariable name);

        internal bool TryGetVariable(string name, out PythonVariable variable) {
            if (Variables != null && name != null) {
                return Variables.TryGetValue(name, out variable);
            } else {
                variable = null;
                return false;
            }
        }

        internal virtual bool TryBindOuter(IBindableNode from, string name, bool allowGlobals, out PythonVariable variable) {
            // Hide scope contents by default (only functions expose their locals)
            variable = null;
            return false;
        }

        internal abstract PythonVariable BindReference(PythonNameBinder binder, string name);

        internal virtual void Bind(PythonNameBinder binder) {
            if (_references != null) {
                foreach (var refList in _references.Values) {
                    foreach (var reference in refList) {
                        PythonVariable variable;
                        reference.Variable = variable = BindReference(binder, reference.Name);

                        // Accessing outer scope variable which is being deleted?
                        if (variable != null) {
                            if (variable.Deleted && variable.ScopeNode != Node && !variable.IsGlobal && binder.LanguageVersion < PythonLanguageVersion.V32) {
                                // report syntax error
                                binder.ReportSyntaxError(
                                    "can not delete variable '{0}' referenced in nested scope"
                                        .FormatUI(reference.Name),
                                    Node);
                            }
                        }
                    }
                }
            }
        }

        internal void FinishBind(PythonNameBinder binder) {
            List<ClosureInfo> closureVariables = null;

            if (_nonLocalVars != null) {
                foreach (var variableName in _nonLocalVars) {
                    var bound = false;
                    for (var parent = Node.ParentNode; parent != null; parent = parent.ParentNode) {
                        PythonVariable variable = null;
                        if ((parent as IBindableNode)?.TryBindOuter(Node, variableName.Name, false, out variable) ?? false) {
                            bound = !variable.IsGlobal;
                            break;
                        }
                    }

                    if (!bound) {
                        binder.ReportSyntaxError("no binding for nonlocal '{0}' found".FormatUI(variableName.Name),
                            variableName);
                    }
                }
            }

            if (FreeVariables != null && FreeVariables.Count > 0) {
                closureVariables = new List<ClosureInfo>();

                foreach (var variable in FreeVariables) {
                    closureVariables.Add(new ClosureInfo(variable, !(Node is ClassDefinition)));
                }
            }

            if (Variables != null && Variables.Count > 0) {
                if (closureVariables == null) {
                    closureVariables = new List<ClosureInfo>();
                }

                foreach (var variable in Variables.Values) {
                    if (!HasClosureVariable(closureVariables, variable) &&
                        !variable.IsGlobal && (variable.AccessedInNestedScope || Node.ExposesLocalVariable(variable))) {
                        closureVariables.Add(new ClosureInfo(variable, true));
                    }

                    if (variable.Kind == VariableKind.Local) {
                        Debug.Assert(variable.ScopeNode == Node);
                    }
                }
            }

            // no longer needed
            _references = null;
        }

        private static bool HasClosureVariable(List<ClosureInfo> closureVariables, PythonVariable variable) {
            if (closureVariables == null) {
                return false;
            }

            for (var i = 0; i < closureVariables.Count; i++) {
                if (closureVariables[i].Variable == variable) {
                    return true;
                }
            }

            return false;
        }

        private void EnsureVariables() {
            if (Variables == null) {
                Variables = new Dictionary<string, PythonVariable>(StringComparer.Ordinal);
            }
        }

        internal PythonReference Reference(string /*!*/ name) {
            if (_references == null) {
                _references = new Dictionary<string, List<PythonReference>>(StringComparer.Ordinal);
            }

            if (!_references.TryGetValue(name, out var references)) {
                _references[name] = references = new List<PythonReference>();
            }
            var reference = new PythonReference(name);
            references.Add(reference);
            return reference;
        }

        internal bool IsReferenced(string name) => _references != null && _references.ContainsKey(name);

        internal PythonVariable CreateVariable(string name, VariableKind kind) {
            EnsureVariables();
            PythonVariable variable;
            Variables[name] = variable = new PythonVariable(name, kind, Node);
            return variable;
        }

        internal void AddVariable(PythonVariable variable) {
            EnsureVariables();
            Variables[variable.Name] = variable;
        }

        internal PythonVariable /*!*/ EnsureVariable(string /*!*/ name) {
            if (!TryGetVariable(name, out var variable)) {
                return CreateVariable(name, VariableKind.Local);
            }

            return variable;
        }

        /// <summary>
        /// Creates a variable at the global level.  Called for known globals (e.g. __name__),
        /// for variables explicitly declared global by the user, and names accessed
        /// but not defined in the lexical scope.
        /// </summary>
        internal PythonVariable /*!*/ EnsureGlobalVariable(string name) {
            if (!TryGetVariable(name, out var variable)) {
                variable = CreateVariable(name, VariableKind.Global);
            }

            return variable;
        }

        internal PythonVariable DefineParameter(string name) => CreateVariable(name, VariableKind.Parameter);

        struct ClosureInfo {
            public PythonVariable Variable;
            public bool AccessedInScope;

            public ClosureInfo(PythonVariable variable, bool accessedInScope) {
                Variable = variable;
                AccessedInScope = accessedInScope;
            }
        }
    }
}
