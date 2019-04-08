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

using System.Linq;
using System.Text;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Analyzer.Caching {
    internal sealed class AnalysisWriter {
        public void WriteModuleData(StringBuilder sb, IPythonModule module) {
            var obj = new[] { module.Interpreter.GetBuiltinType(BuiltinTypeId.Object) };
            foreach (var v in module.GlobalScope.Variables) {
                var t = v.Value.GetPythonType();
                if (t.DeclaringModule != module || v.Name.StartsWith("__")) {
                    continue;
                }

                switch (t) {
                    case IPythonClassType cls:
                        WriteScope(sb, indent + 4, cls.ClassDefinition);
                        break;

                    case IPythonFunctionType ft:
                        var o = ft.Overloads.FirstOrDefault();
                        if (o == null) {
                            continue;
                        }

                        var sbp = new StringBuilder();
                        var count = 0;
                        foreach (var p in o.Parameters) {
                            sbp.AppendIf(count > 0, ", ");
                            sbp.Append(p.Name);
                            if (!p.Type.IsUnknown()) {
                                sbp.Append($": {p.Type.Name}");
                            }
                            count++;
                        }

                        var paramString = sbp.ToString();
                        sb.Append($"{new string(' ', indent)}def {ft.Name}({paramString})");
                        var retType = o.StaticReturnValue?.GetPythonType();
                        sb.AppendLine(retType?.IsUnknown() == false ? $" -> {retType.Name}: ..." : ": ...");
                        break;

                    case IPythonPropertyType prop:
                        sb.AppendLine("@property");
                        sb.Append($"{new string(' ', indent)}def {prop.Name}(self)");
                        var propType = prop.Type;
                        sb.AppendLine(propType?.IsUnknown() == false ? $" -> {propType.Name}: ..." : ": ...");
                        break;
                }
            }
        }

    }
}
