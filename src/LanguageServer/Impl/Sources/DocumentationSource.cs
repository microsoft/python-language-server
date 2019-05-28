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
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core.Text;

namespace Microsoft.Python.LanguageServer.Sources {
    internal abstract class DocumentationSource {
        public string GetSignatureString(IPythonFunctionType ft, IPythonType self, out IndexSpan[] parameterSpans, int overloadIndex = 0, string name = null) {
            var o = ft.Overloads[overloadIndex];

            var parameterStrings = GetFunctionParameters(ft, out var parameterNameLengths);
            var returnDoc = o.GetReturnDocumentation(self);
            var annString = string.IsNullOrEmpty(returnDoc) ? string.Empty : $" -> {returnDoc}";

            // Calculate parameter spans
            parameterSpans = new IndexSpan[parameterStrings.Length];
            name = name ?? ft.Name;
            var offset = name.Length + 1;
            for (var i = 0; i < parameterStrings.Length; i++) {
                parameterSpans[i] = IndexSpan.FromBounds(offset, offset + parameterNameLengths[i]);
                offset += parameterStrings[i].Length + 2; // name,<space>
            }

            var combinedParameterString = string.Join(", ", parameterStrings);
            return $"{name}({combinedParameterString}){annString}";
        }

        /// <summary>
        /// Constructs parameter strings that include parameter name, type and default value
        /// like 'x: int' or 'a = None'. Returns collection of parameter strings and
        /// the respective lengths of names for rendering in bold (as current parameter).
        /// </summary>
        private string[] GetFunctionParameters(IPythonFunctionType ft, out int[] parameterNameLengths, int overloadIndex = 0) {
            var o = ft.Overloads[overloadIndex]; // TODO: display all?
            var skip = ft.IsStatic || ft.IsUnbound ? 0 : 1;

            var count = Math.Max(0, o.Parameters.Count - skip);
            var parameters = new string[count];
            parameterNameLengths = new int[count];
            for (var i = skip; i < o.Parameters.Count; i++) {
                string paramString;
                var p = o.Parameters[i];
                if (!string.IsNullOrEmpty(p.DefaultValueString)) {
                    paramString = $"{p.Name}={p.DefaultValueString}";
                } else {
                    paramString = p.Type.IsUnknown() ? p.Name : $"{p.Name}: {p.Type.Name}";
                }
                parameters[i - skip] = paramString;
                parameterNameLengths[i - skip] = p.Name.Length;
            }
            return parameters;
        }
    }
}
