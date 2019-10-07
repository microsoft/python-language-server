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
using System.Linq;
using System.Numerics;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Evaluation {
    internal sealed partial class ExpressionEval {
        public IPythonInstance GetConstantFromLiteral(Expression expr) {
            if (expr is ConstantExpression ce) {
                switch (ce.Value) {
                    case string s:
                        return new PythonUnicodeString(s, Interpreter);
                    case AsciiString b:
                        return new PythonAsciiString(b, Interpreter);
                    case int integer:
                        return new PythonConstant(integer, Interpreter.GetBuiltinType(BuiltinTypeId.Int));
                    case bool b:
                        return new PythonConstant(b, Interpreter.GetBuiltinType(BuiltinTypeId.Bool));
                }
            }

            var t = SuppressBuiltinLookup ? UnknownType : (GetTypeFromLiteral(expr) ?? UnknownType);
            return t.CreateInstance(ArgumentSet.Empty(expr, this));
        }

        public IPythonType GetTypeFromLiteral(Expression expr) {
            if (expr is ConstantExpression ce) {
                if (ce.Value == null) {
                    return Interpreter.GetBuiltinType(BuiltinTypeId.None);
                }

                switch (Type.GetTypeCode(ce.Value.GetType())) {
                    case TypeCode.Boolean: return Interpreter.GetBuiltinType(BuiltinTypeId.Bool);
                    case TypeCode.Double: return Interpreter.GetBuiltinType(BuiltinTypeId.Float);
                    case TypeCode.Int32: return Interpreter.GetBuiltinType(BuiltinTypeId.Int);
                    case TypeCode.String: return Interpreter.GetBuiltinType(BuiltinTypeId.Unicode);
                    case TypeCode.Object:
                        switch (ce.Value) {
                            case Complex _:
                                return Interpreter.GetBuiltinType(BuiltinTypeId.Complex);
                            case AsciiString _:
                                return Interpreter.GetBuiltinType(BuiltinTypeId.Bytes);
                            case BigInteger _:
                                return Interpreter.GetBuiltinType(BuiltinTypeId.Long);
                            case Ellipsis _:
                                return Interpreter.GetBuiltinType(BuiltinTypeId.Ellipsis);
                        }

                        break;
                }

                return null;
            }

            if (expr is ListExpression || expr is ListComprehension) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.List);
            }

            if (expr is DictionaryExpression || expr is DictionaryComprehension) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.Dict);
            }

            if (expr is TupleExpression tex) {
                var types = tex.Items
                    .Select(x => {
                        IPythonType value = null;
                        if (x is NameExpression ne) {
                            value = GetInScope(ne.Name)?.GetPythonType();
                        }

                        return value ?? UnknownType;
                    }).ToArray();
                var res = Interpreter.GetBuiltinType(BuiltinTypeId.Tuple);
                if (types.Length > 0) {
                    var iterRes = Interpreter.GetBuiltinType(BuiltinTypeId.TupleIterator);
                    //res = new PythonSequence(res, Module, types, iterRes);
                }

                return res;
            }

            if (expr is SetExpression || expr is SetComprehension) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.Set);
            }

            if (expr is BackQuoteExpression && Interpreter.LanguageVersion.Is2x()) {
                return Interpreter.GetBuiltinType(BuiltinTypeId.Bytes);
            }

            return expr is LambdaExpression ? Interpreter.GetBuiltinType(BuiltinTypeId.Function) : null;
        }
    }
}
