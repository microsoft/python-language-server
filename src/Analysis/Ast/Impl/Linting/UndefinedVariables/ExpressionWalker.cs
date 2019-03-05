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

using System.Collections.Generic;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Linting.UndefinedVariables {
    internal sealed class ExpressionWalker : PythonWalker {
        private readonly IDocumentAnalysis _analysis;
        private readonly HashSet<string> _additionalNames;
        private readonly HashSet<NameExpression> _additionalNameNodes;

        public ExpressionWalker(IDocumentAnalysis analysis)
            : this(analysis, null, null) { }

        /// <summary>
        /// Creates walker for detection of undefined variables.
        /// </summary>
        /// <param name="analysis">Document analysis.</param>
        /// <param name="additionalNames">Additional defined names.</param>
        /// <param name="additionalNameNodes">Name nodes for defined names.</param>
        public ExpressionWalker(IDocumentAnalysis analysis, HashSet<string> additionalNames, HashSet<NameExpression> additionalNameNodes) {
            _analysis = analysis;
            _additionalNames = additionalNames;
            _additionalNameNodes = additionalNameNodes;
        }

        public override bool Walk(CallExpression node) {
            foreach (var arg in node.Args) {
                arg?.Expression?.Walk(this);
            }
            return false;
        }

        public override bool Walk(ListComprehension node) {
            node.Walk(new ComprehensionWalker(_analysis));
            return false;
        }

        public override bool Walk(SetComprehension node) {
            node.Walk(new ComprehensionWalker(_analysis));
            return false;
        }
        public override bool Walk(DictionaryComprehension node) {
            node.Walk(new ComprehensionWalker(_analysis));
            return false;
        }

        public override bool Walk(GeneratorExpression node) {
            node.Walk(new ComprehensionWalker(_analysis));
            return false;
        }

        public override bool Walk(NameExpression node) {
            if (_additionalNames?.Contains(node.Name) == true) {
                return false;
            }
            if (_additionalNameNodes?.Contains(node) == true) {
                return false;
            }
            var m = _analysis.ExpressionEvaluator.LookupNameInScopes(node.Name, out _);
            if (m == null) {
                _analysis.ReportUndefinedVariable(node);
            }
            // Take into account where variable is defined so we do detect
            // undefined x in 
            //    y = x
            //    x = 1
            if(m is ILocatedMember lm && lm.Location.DocumentUri == _analysis.Document.Uri) {
                if (!(m is IPythonFunctionType || m is IPythonClassType)) {
                    var span = lm.Location.Span;
                    var nodeLoc = node.GetLocation(_analysis.Document);
                    if (span.IsAfter(nodeLoc.Span)) {
                        _analysis.ReportUndefinedVariable(node);
                    }
                }
            }
            return false;
        }
    }
}
