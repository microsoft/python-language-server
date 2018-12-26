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

            return new PythonSequenceInstance(contents, Interpreter, GetLoc(expression));
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
            if (target is IPythonSequenceInstance instance) {
                var m = await GetValueFromExpressionAsync(expr.Index, cancellationToken);
                var index = 0;
                if (m is IPythonConstant c) {
                    if (c.Type.TypeId == BuiltinTypeId.Int || c.Type.TypeId == BuiltinTypeId.Long) {
                        index = (int)c.Value;
                    } else {
                        // TODO: report bad index type.
                        return UnknownType;
                    }
                }
                return instance.GetValueAt(index);
                // TODO: handle typing module
            }
            return UnknownType;
        }
    }
}
