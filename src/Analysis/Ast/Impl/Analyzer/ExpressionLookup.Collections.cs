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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    internal sealed partial class ExpressionLookup {
        private async Task<IMember> GetValueFromListAsync(ListExpression expression, CancellationToken cancellationToken = default) {
            var contents = new List<IMember>();
            foreach (var item in expression.Items) {
                var value = await GetValueFromExpressionAsync(item, cancellationToken) ?? UnknownType;
                contents.Add(value);
            }
            return new PythonList(Interpreter, contents, GetLoc(expression));
        }

        private async Task<IMember> GetValueFromIndexAsync(IndexExpression expr, CancellationToken cancellationToken = default) {
            if (expr?.Target == null) {
                return null;
            }

            if (expr.Index is SliceExpression || expr.Index is TupleExpression) {
                // When slicing, assume result is the same type
                return await GetValueFromExpressionAsync(expr.Target, cancellationToken);
            }

            var target = await GetValueFromExpressionAsync(expr.Target, cancellationToken);
            switch (target) {
                case IPythonSequence seq:
                    return await GetValueFromSequenceInstanceAsync(expr, seq, cancellationToken);
                case ITypedSequenceType seqt:
                    return await GetValueFromSequenceTypeAsync(expr, seqt, cancellationToken);
                default:
                    return UnknownType;
            }
        }

        private async Task<IMember> GetValueFromSequenceInstanceAsync(IndexExpression expr, IPythonSequence seq, CancellationToken cancellationToken = default) {
            var index = await GetIndexFromConstantAsync(expr.Index, cancellationToken);
            return seq.GetValueAt(index);
        }

        private async Task<IMember> GetValueFromSequenceTypeAsync(IndexExpression expr, ITypedSequenceType seqt, CancellationToken cancellationToken = default) {
            if (seqt.ContentTypes.Count == 1) {
                return seqt.ContentTypes[0];
            }
            var index = await GetIndexFromConstantAsync(expr.Index, cancellationToken);
            return index >= 0 && index < seqt.ContentTypes.Count ? seqt.ContentTypes[index] : UnknownType;
        }

        private async Task<int> GetIndexFromConstantAsync(Expression expr, CancellationToken cancellationToken = default) {
            var m = await GetValueFromExpressionAsync(expr, cancellationToken);
            if (m is IPythonConstant c) {
                if (c.Type.TypeId == BuiltinTypeId.Int || c.Type.TypeId == BuiltinTypeId.Long) {
                    return (int)c.Value;
                }
            }
            // TODO: report bad index type.
            return -1;
        }
    }
}
