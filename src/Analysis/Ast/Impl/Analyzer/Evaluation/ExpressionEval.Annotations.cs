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

using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Evaluation {
    internal sealed partial class ExpressionEval {
        public IPythonType GetTypeFromAnnotation(Expression expr, LookupOptions options = LookupOptions.Global | LookupOptions.Builtins)
            => GetTypeFromAnnotation(expr, out _, options);

        public IPythonType GetTypeFromAnnotation(Expression expr, out bool isGeneric, LookupOptions options = LookupOptions.Global | LookupOptions.Builtins) {
            isGeneric = false;
            switch (expr) {
                case null:
                    return null;
                case NameExpression nameExpr:
                    // x: T
                    var name = GetValueFromExpression(nameExpr);
                    if(name is IGenericTypeParameter gtp) {
                        isGeneric = true;
                        return gtp;
                    }
                    break;
                case CallExpression callExpr:
                    // x: NamedTuple(...)
                    return GetValueFromCallable(callExpr)?.GetPythonType() ?? UnknownType;
                case IndexExpression indexExpr:
                    // Try generics
                    var target = GetValueFromExpression(indexExpr.Target);
                    var result = GetValueFromGeneric(target, indexExpr);
                    if (result != null) {
                        isGeneric = true;
                        return result.GetPythonType();
                    }
                    break;
            }

            // Look at specialization and typing first
            var ann = new TypeAnnotation(Ast.LanguageVersion, expr);
            return ann.GetValue(new TypeAnnotationConverter(this, expr, options));
        }
    }
}
