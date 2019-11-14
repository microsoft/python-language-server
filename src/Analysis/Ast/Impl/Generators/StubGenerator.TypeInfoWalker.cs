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

using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Generators {
    public sealed partial class StubGenerator {
        private sealed class TypeInfoWalker : BaseWalker {
            private readonly IExpressionEvaluator _eval;

            public TypeInfoWalker(ILogger logger, IPythonModule module, PythonAst ast, string original)
                : base(logger, module, ast, original) {
                _eval = Module.Analysis.ExpressionEvaluator;
            }

            public override bool Walk(ClassDefinition node, Node parent) {
                return base.Walk(node, parent);
            }

            public override bool Walk(FunctionDefinition node, Node parent) {
                var member = _eval.LookupNameInScopes(node.Name, LookupOptions.All);
                if (member != null) {
                    // see whether we can improve generated code with our data from analysis
                }

                return base.Walk(node, parent);
            }
        }
    }
}
