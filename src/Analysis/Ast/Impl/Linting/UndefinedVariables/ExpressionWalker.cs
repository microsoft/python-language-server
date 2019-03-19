﻿// Copyright(c) Microsoft Corporation
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
using System.Linq;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Linting.UndefinedVariables {
    internal sealed class ExpressionWalker : PythonWalker {
        private readonly UndefinedVariablesWalker _walker;
        private readonly HashSet<string> _localNames;
        private readonly HashSet<NameExpression> _localNameNodes;

        public ExpressionWalker(UndefinedVariablesWalker walker)
            : this(walker, null, null) { }

        /// <summary>
        /// Creates walker for detection of undefined variables.
        /// </summary>
        /// <param name="walker">Undefined variables walker.</param>
        /// <param name="localNames">Locally defined names, such as variables in a comprehension.</param>
        /// <param name="localNameNodes">Name nodes for local names.</param>
        public ExpressionWalker(UndefinedVariablesWalker walker, HashSet<string> localNames, HashSet<NameExpression> localNameNodes) {
            _walker = walker;
            _localNames = localNames;
            _localNameNodes = localNameNodes;
        }

        public override bool Walk(CallExpression node) {
            foreach (var arg in node.Args) {
                arg?.Expression?.Walk(this);
            }
            return false;
        }

        public override bool Walk(LambdaExpression node) {
            node.Walk(new LambdaWalker(_walker));
            return false;
        }

        public override bool Walk(ListComprehension node) {
            node.Walk(new ComprehensionWalker(_walker, _localNames, _localNameNodes));
            return false;
        }

        public override bool Walk(SetComprehension node) {
            node.Walk(new ComprehensionWalker(_walker, _localNames, _localNameNodes));
            return false;
        }
        public override bool Walk(DictionaryComprehension node) {
            node.Walk(new ComprehensionWalker(_walker, _localNames, _localNameNodes));
            return false;
        }

        public override bool Walk(GeneratorExpression node) {
            node.Walk(new ComprehensionWalker(_walker, _localNames, _localNameNodes));
            return false;
        }

        public override bool Walk(NameExpression node) {
            if (_localNames?.Contains(node.Name) == true) {
                return false;
            }
            if (_localNameNodes?.Contains(node) == true) {
                return false;
            }

            var analysis = _walker.Analysis;
            var m = analysis.ExpressionEvaluator.LookupNameInScopes(node.Name, out var scope);
            if (m == null) {
                _walker.ReportUndefinedVariable(node);
            }
            // Take into account where variable is defined so we do detect
            // undefined x in 
            //    y = x
            //    x = 1
            var v = scope?.Variables[node.Name];
            if (v != null && v.Location.DocumentUri == analysis.Document.Uri) {
                // Do not complain about functions and classes that appear later in the file
                if (!(v.Value is IPythonFunctionType || v.Value is IPythonClassType)) {
                    var span = v.Locations.First().Span;
                    var nodeLoc = node.GetLocation(analysis.Document);
                    // Exclude same-name variables declared within the same statement
                    // like 'e' that appears before its declaration in '[e in for e in {}]'
                    if (span.IsAfter(nodeLoc.Span) && !IsSpanInComprehension(nodeLoc.Span)) {
                        _walker.ReportUndefinedVariable(node);
                    }
                }
            }
            return false;
        }

        private bool IsSpanInComprehension(SourceSpan span) {
            var start = span.Start.ToIndex(_walker.Analysis.Ast);
            var end = span.End.ToIndex(_walker.Analysis.Ast);
            return ((Node)_walker.Analysis.ExpressionEvaluator.CurrentScope.Node)
                .TraverseDepthFirst(n => n.GetChildNodes())
                .OfType<Comprehension>()
                .Any(n => n.StartIndex <= start && end < n.EndIndex);
        }
    }
}
