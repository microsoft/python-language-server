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
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Protocol;

namespace Microsoft.Python.LanguageServer.Sources {
    internal abstract class DocumentationSource {
        public string GetSignatureString(IPythonFunctionType ft, IPythonType self, out IndexSpan[] parameterSpans, int overloadIndex = 0, string name = null) {
            var o = ft.Overloads[overloadIndex];

            var parms = GetFunctionParameters(ft).ToArray();
            var parmString = string.Join(", ", parms);
            var returnDoc = o.GetReturnDocumentation(self);
            var annString = string.IsNullOrEmpty(returnDoc) ? string.Empty : $" -> {returnDoc}";

            // Calculate parameter spans
            parameterSpans = new IndexSpan[parms.Length];
            name = name ?? ft.Name;
            var offset = name.Length + 1;
            for(var i = 0; i < parms.Length; i++) {
                parameterSpans[i]= IndexSpan.FromBounds(offset, offset + parms[i].Length);
                offset += parms[i].Length + 2; // name,<space>
            }

            return $"{name}({parmString}){annString}";
        }

        private IEnumerable<string> GetFunctionParameters(IPythonFunctionType ft, int overloadIndex = 0) {
            var o = ft.Overloads[overloadIndex]; // TODO: display all?
            var skip = ft.IsStatic || ft.IsUnbound ? 0 : 1;
            return o.Parameters.Skip(skip).Select(p => {
                if (!string.IsNullOrEmpty(p.DefaultValueString)) {
                    return $"{p.Name}={p.DefaultValueString}";
                }
                return p.Type.IsUnknown() ? p.Name : $"{p.Name}: {p.Type.Name}";
            });
        }
    }
}
