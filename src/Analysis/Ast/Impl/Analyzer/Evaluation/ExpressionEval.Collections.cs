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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Types.Collections;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Analysis.Values.Collections;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Evaluation {
    internal sealed partial class ExpressionEval {
        public async Task<IMember> GetValueFromIndexAsync(IndexExpression expr, CancellationToken cancellationToken = default) {
            if (expr?.Target == null) {
                return null;
            }

            var target = await GetValueFromExpressionAsync(expr.Target, cancellationToken);
            // If target is a generic type, create specific class
            if (target is IGenericType gen) {
                return await CreateSpecificFromGenericAsync(gen, expr, cancellationToken);
            }

            if (expr.Index is SliceExpression || expr.Index is TupleExpression) {
                // When slicing, assume result is the same type
                return target;
            }

            var type = target.GetPythonType();
            if (type != null) {
                if (!(target is IPythonInstance instance)) {
                    instance = new PythonInstance(type);
                }
                var index = await GetValueFromExpressionAsync(expr.Index, cancellationToken);
                return type.Index(instance, index);
            }

            return UnknownType;
        }

        public async Task<IMember> GetValueFromListAsync(ListExpression expression, CancellationToken cancellationToken = default) {
            var contents = new List<IMember>();
            foreach (var item in expression.Items) {
                var value = await GetValueFromExpressionAsync(item, cancellationToken) ?? UnknownType;
                contents.Add(value);
            }
            return PythonCollectionType.CreateList(Module.Interpreter, GetLoc(expression), contents);
        }

        public async Task<IMember> GetValueFromDictionaryAsync(DictionaryExpression expression, CancellationToken cancellationToken = default) {
            var contents = new Dictionary<IMember, IMember>();
            foreach (var item in expression.Items) {
                var key = await GetValueFromExpressionAsync(item.SliceStart, cancellationToken) ?? UnknownType;
                var value = await GetValueFromExpressionAsync(item.SliceStop, cancellationToken) ?? UnknownType;
                contents[key] = value;
            }
            return new PythonDictionary(Interpreter, GetLoc(expression), contents);
        }

        private async Task<IMember> GetValueFromTupleAsync(TupleExpression expression, CancellationToken cancellationToken = default) {
            var contents = new List<IMember>();
            foreach (var item in expression.Items) {
                var value = await GetValueFromExpressionAsync(item, cancellationToken) ?? UnknownType;
                contents.Add(value);
            }
            return PythonCollectionType.CreateTuple(Module.Interpreter, GetLoc(expression), contents);
        }

        public async Task<IMember> GetValueFromSetAsync(SetExpression expression, CancellationToken cancellationToken = default) {
            var contents = new List<IMember>();
            foreach (var item in expression.Items) {
                var value = await GetValueFromExpressionAsync(item, cancellationToken) ?? UnknownType;
                contents.Add(value);
            }
            return PythonCollectionType.CreateSet(Interpreter, GetLoc(expression), contents);
        }

        public async Task<IMember> GetValueFromGeneratorAsync(GeneratorExpression expression, CancellationToken cancellationToken = default) {
            var iter = expression.Iterators.OfType<ComprehensionFor>().FirstOrDefault();
            if(iter != null) { 
                return await GetValueFromExpressionAsync(iter.List, cancellationToken) ?? UnknownType;
            }
            return UnknownType;
        }


        private async Task<IMember> CreateSpecificFromGenericAsync(IGenericType gen, IndexExpression expr, CancellationToken cancellationToken = default) {
            var args = new List<IPythonType>();
            if (expr.Index is TupleExpression tex) {
                foreach (var item in tex.Items) {
                    var e = await GetValueFromExpressionAsync(item, cancellationToken);
                    args.Add(e?.GetPythonType() ?? UnknownType);
                }
            } else {
                var index = await GetValueFromExpressionAsync(expr.Index, cancellationToken);
                args.Add(index?.GetPythonType() ?? UnknownType);
            }
            return gen.CreateSpecificType(args, Module, GetLoc(expr));
        }

    }
}
