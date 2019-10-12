using System.Collections.Generic;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Parsing {
    public static class ScopeExtensions {
        public static ScopeStatement FindClosestScopeStatement(this IScopeNode node) {
            var scope = node;
            while (scope != null && !(scope is ScopeStatement)) {
                scope = scope.ParentScopeNode;
            }
            return scope as ScopeStatement;
        }

        public static IEnumerable<IScopeNode> EnumerateTowardsGlobal(this IScopeNode node) {
            for (var parent = node.ParentScopeNode; parent != null; parent = parent.ParentScopeNode) {
                yield return parent;
            }
        }
    }
}
