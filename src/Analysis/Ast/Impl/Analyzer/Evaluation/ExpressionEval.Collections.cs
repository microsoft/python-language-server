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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Types.Collections;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Analysis.Values.Collections;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Evaluation {
    internal sealed partial class ExpressionEval {
        public IMember GetValueFromIndex(IndexExpression expr) {
            if (expr?.Target == null) {
                return null;
            }

            var target = GetValueFromExpression(expr.Target);
            // Try generics first since this may be an expression like Dict[int, str]
            var result = GetValueFromGeneric(target, expr);
            if (result != null) {
                return result;
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
                var index = GetValueFromExpression(expr.Index);
                if (index != null) {
                    return type.Index(instance, index);
                }
            }

            return UnknownType;
        }

        public IMember GetValueFromList(ListExpression expression) {
            var contents = new List<IMember>();
            foreach (var item in expression.Items) {
                var value = GetValueFromExpression(item) ?? UnknownType;
                contents.Add(value);
            }
            return PythonCollectionType.CreateList(Module.Interpreter, GetLoc(expression), contents);
        }

        public IMember GetValueFromDictionary(DictionaryExpression expression) {
            var contents = new Dictionary<IMember, IMember>();
            foreach (var item in expression.Items) {
                var key = GetValueFromExpression(item.SliceStart) ?? UnknownType;
                var value = GetValueFromExpression(item.SliceStop) ?? UnknownType;
                contents[key] = value;
            }
            return new PythonDictionary(Interpreter, GetLoc(expression), contents);
        }

        private IMember GetValueFromTuple(TupleExpression expression) {
            var contents = new List<IMember>();
            foreach (var item in expression.Items) {
                var value = GetValueFromExpression(item) ?? UnknownType;
                contents.Add(value);
            }
            return PythonCollectionType.CreateTuple(Module.Interpreter, GetLoc(expression), contents);
        }

        public IMember GetValueFromSet(SetExpression expression) {
            var contents = new List<IMember>();
            foreach (var item in expression.Items) {
                var value = GetValueFromExpression(item) ?? UnknownType;
                contents.Add(value);
            }
            return PythonCollectionType.CreateSet(Interpreter, GetLoc(expression), contents);
        }

        public IMember GetValueFromGenerator(GeneratorExpression expression) {
            var iter = expression.Iterators.OfType<ComprehensionFor>().FirstOrDefault();
            if (iter != null) {
                return GetValueFromExpression(iter.List) ?? UnknownType;
            }
            return UnknownType;
        }
    }
}
