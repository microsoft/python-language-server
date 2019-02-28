﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Python.Parsing.Ast {
    public class FStringExpression : Expression {
        public readonly List<Expression> Children;

        public FStringExpression(List<Expression> children) {
            Children = children;
        }

        public override IEnumerable<Node> GetChildNodes() {
            return Children;
        }

        public override void Walk(PythonWalker walker) {
            walker.Walk(this);
            foreach (var child in Children) {
                child.Walk(walker);
            }
            walker.PostWalk(this);
        }

        public override Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            throw new NotImplementedException();
        }
    }
}
