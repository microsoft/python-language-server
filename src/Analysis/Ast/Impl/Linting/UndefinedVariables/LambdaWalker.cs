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

using System.Linq;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Linting.UndefinedVariables {
    internal sealed class LambdaWalker : PythonWalker {
        private readonly UndefinedVariablesWalker _walker;

        public LambdaWalker(UndefinedVariablesWalker walker) {
            _walker = walker;
        }

        public override bool Walk(FunctionDefinition node) {
            var nc = CollectNames(node);
            node.Body?.Walk(new ExpressionWalker(_walker, nc.Names, nc.NameExpressions));
            return false;
        }

        private NameCollectorWalker CollectNames(FunctionDefinition fd) {
            var nc = new NameCollectorWalker();
            foreach (var nex in fd.Parameters.Select(p => p.NameExpression).ExcludeDefault()) {
                nex.Walk(nc);
            }
            return nc;
        }
    }
}
