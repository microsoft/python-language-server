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

using System;
using Microsoft.Python.Analysis.Analyzer.Handlers;
using Microsoft.Python.Analysis.Analyzer.Symbols;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Evaluation {
    /// <summary>
    /// Evaluates call to a function with a specific set of arguments.
    /// </summary>
    internal sealed class FunctionCallEvaluator: AnalysisWalker {
        private readonly ExpressionEval _eval;
        private readonly IPythonModule _declaringModule;
        private readonly FunctionDefinition _function;
        private IMember _result;

        public FunctionCallEvaluator(IPythonModule declaringModule, FunctionDefinition fd, ExpressionEval eval): base(eval, SimpleImportedVariableHandler.Instance) {
            _declaringModule = declaringModule ?? throw new ArgumentNullException(nameof(declaringModule));
            _eval = eval ?? throw new ArgumentNullException(nameof(eval));
            _function = fd ?? throw new ArgumentNullException(nameof(fd));
        }

        /// <summary>
        /// Evaluates call to a function with a specific set of arguments.
        /// Intended to be used when function return type was not possible
        /// to determine from its return annotation or via basic evaluation.
        /// </summary>
        /// <remarks>
        /// This is different from evaluation in <see cref="FunctionEvaluator"/>
        /// that that checks for the return type annotation and attempts to determine
        /// static return type.
        /// </remarks>
        public IMember EvaluateCall(IArgumentSet args) {
            // Open scope and declare parameters
            using (_eval.OpenScope(_declaringModule, _function, out _)) {
                args.DeclareParametersInScope(_eval);
                _function.Body?.Walk(this);
            }
            return _result;
        }

        public override bool Walk(ReturnStatement node) {
            var value = Eval.GetValueFromExpression(node.Expression, LookupOptions.Normal);
            if (!value.IsUnknown()) {
                _result = value;
                return false;
            }
            return true;
        }
    }
}
