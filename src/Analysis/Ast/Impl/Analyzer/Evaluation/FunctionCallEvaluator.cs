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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Analyzer.Symbols;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Types.Collections;
using Microsoft.Python.Analysis.Values.Collections;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Evaluation {
    /// <summary>
    /// Evaluates call to a function with a specific set of arguments.
    /// </summary>
    internal sealed class FunctionCallEvaluator: AnalysisWalker {
        private readonly ExpressionEval _eval;
        private readonly FunctionDefinition _function;
        private readonly IPythonInterpreter _interpreter;
        private IMember _result;

        public FunctionCallEvaluator(FunctionDefinition fd, ExpressionEval eval, IPythonInterpreter interpreter): base(eval) {
            _eval = eval ?? throw new ArgumentNullException(nameof(eval));
            _function = fd ?? throw new ArgumentNullException(nameof(fd));
            _interpreter = interpreter;
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
        public async Task<IMember> EvaluateCallAsync(IArgumentSet args, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();

            // Open scope and declare parameters
            using (_eval.OpenScope(_function, out _)) {
                args.DeclareParametersInScope(_eval);
                await _function.Body.WalkAsync(this, cancellationToken);
            }
            return _result;
        }

        public override async Task<bool> WalkAsync(AssignmentStatement node, CancellationToken cancellationToken = default) {
            foreach (var lhs in node.Left) {
                if (lhs is NameExpression nameExp && (nameExp.Name == "self" || nameExp.Name == "cls")) {
                    return true; // Don't assign to 'self' or 'cls'.
                }
            }
            return await base.WalkAsync(node, cancellationToken);
        }

        public override async Task<bool> WalkAsync(ReturnStatement node, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();

            var value = await Eval.GetValueFromExpressionAsync(node.Expression, cancellationToken);
            if (!value.IsUnknown()) {
                _result = value;
                return false;
            }
            return true;
        }
    }
}
