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

namespace Microsoft.Python.Analysis.Analyzer.Evaluation {
    internal sealed partial class ExpressionEval {
        private async Task<IMember> GetValueFromListAsync(ListExpression expression, CancellationToken cancellationToken = default) {
            var contents = new List<IMember>();
            foreach (var item in expression.Items) {
                var value = await GetValueFromExpressionAsync(item, cancellationToken) ?? UnknownType;
                contents.Add(value);
            }
            return PythonListType.GetPythonListType(Interpreter).CreateInstance(Module, GetLoc(expression), contents);
        }

        private async Task<IMember> GetValueFromIndexAsync(IndexExpression expr, CancellationToken cancellationToken = default) {
            if (expr?.Target == null) {
                return null;
            }

            if (expr.Index is SliceExpression || expr.Index is TupleExpression) {
                // When slicing, assume result is the same type
                return await GetValueFromExpressionAsync(expr.Target, cancellationToken);
            }

            IMember result = null;
            if (await GetValueFromExpressionAsync(expr.Target, cancellationToken) is IPythonInstance instance) {
                var index = await GetValueFromExpressionAsync(expr.Index, cancellationToken);
                var type = instance.GetPythonType();
                result = type?.Index(instance, index);
            }
            return result ?? UnknownType;
        }

        private async Task<IMember> GetValueFromDictionaryAsync(DictionaryExpression expression, CancellationToken cancellationToken = default) {
            var contents = new Dictionary<IMember, IMember>();
            foreach (var item in expression.Items) {
                var key = await GetValueFromExpressionAsync(item.SliceStart, cancellationToken) ?? UnknownType;
                var value = await GetValueFromExpressionAsync(item.SliceStop, cancellationToken) ?? UnknownType;
                contents[key] = value;
            }
            return PythonDictionaryType.GetPythonDictionaryType(Interpreter).CreateInstance(Module, GetLoc(expression), contents);
        }
    }
}
