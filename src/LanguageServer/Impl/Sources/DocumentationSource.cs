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
using System.Text;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Sources {
    internal abstract class DocumentationSource {
        public string GetSignatureString(IPythonFunctionType ft, IPythonType self, out (IndexSpan, IParameterInfo)[] parameterSpans, int overloadIndex = 0, string funcName = null) {
            funcName = funcName ?? ft.Name;
            var o = ft.Overloads[overloadIndex];

            var spans = new List<(IndexSpan, IParameterInfo)>();
            var builder = new StringBuilder(funcName);
            builder.Append('(');

            var addComma = false;
            foreach (var p in o.Parameters.Skip(ft.IsStatic || ft.IsUnbound ? 0 : 1)) {
                if (addComma) {
                    builder.Append(", ");
                } else {
                    addComma = true;
                }

                switch (p.Kind) {
                    case ParameterKind.PositionalMarker:
                        builder.Append("/");
                        continue;
                    case ParameterKind.List:
                        builder.Append("*");
                        if (string.IsNullOrEmpty(p.Name)) {
                            continue;
                        }
                        break;
                    case ParameterKind.Dictionary:
                        builder.Append("**");
                        break;
                }

                var name = p.Name ?? string.Empty;
                spans.Add((new IndexSpan(builder.Length, name.Length), p));
                builder.Append(name);

                if (!string.IsNullOrEmpty(p.DefaultValueString)) {
                    builder.Append('=');
                    builder.Append(p.DefaultValue);
                } else if (!p.Type.IsUnknown()) {
                    builder.Append(": ");
                    builder.Append(p.Type.Name);
                }
            }

            builder.Append(')');

            var returnDoc = o.GetReturnDocumentation(self);
            if (!string.IsNullOrWhiteSpace(returnDoc)) {
                builder.Append(" -> ");
                builder.Append(returnDoc);
            }

            parameterSpans = spans.ToArray();
            return builder.ToString();
        }
    }
}
