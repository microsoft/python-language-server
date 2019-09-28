using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Parsing {
    public static class ScopeExtensions {
        public static ScopeStatement FindClosestScopeStatement(this IScopeNode node) {
            var scope = node;
            while (scope != null && !(scope is ScopeStatement)) {
                scope = scope.ParentNode;
            }
            return scope as ScopeStatement;
        }
    }
}
