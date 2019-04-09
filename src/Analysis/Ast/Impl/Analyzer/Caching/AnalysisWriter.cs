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

using System.Text;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Analyzer.Caching {
    internal sealed class AnalysisWriter {
        private readonly StringBuilder _sb = new StringBuilder();
        private readonly IPythonModule _module;

        public AnalysisWriter(IPythonModule module) {
            _module = module;
        }

        public string WriteModuleData() {
            // Write original module info as a comment
            _sb.AppendLine($"# {_module.Name} [{_module.FilePath}]");

            foreach (var v in _module.GlobalScope.Variables) {
                var t = v.Value.GetPythonType();
                if (t.DeclaringModule != _module || v.Name.StartsWith("__")) {
                    continue;
                }

                switch (t) {
                    case IPythonClassType cls:
                        WriteClass(cls);
                        break;

                    case IPythonFunctionType ft:
                        WriteFunction(ft);
                        break;

                    case IPythonPropertyType prop:
                        WriteProperty(prop);
                        break;
                }
            }

            var s = _sb.ToString();
            return s;
        }

        private void WriteClass(IPythonClassType cls) {
            _sb.AppendLine();
            foreach (var name in cls.GetMemberNames()) {
                var m = cls.GetMember(name);
                switch (m) {
                    case IPythonFunctionType ft:
                        WriteFunction(ft);
                        break;

                    case IPythonPropertyType prop:
                        WriteProperty(prop);
                        break;

                    case IPythonInstance inst:
                        WriteInstance(cls.FullyQualifiedName, name, inst);
                        break;
                }
            }
            _sb.AppendLine();
        }

        private void WriteFunction(IPythonFunctionType ft) {
            if (ft.Overloads.Count == 0) {
                return;
            }

            if (ft.Overloads.Count == 1) {
                WriteOverload(ft.FullyQualifiedName, ft.Overloads[0], -1);
                return;
            }
            for (var i = 0; i < ft.Overloads.Count; i++) {
                WriteOverload(ft.FullyQualifiedName, ft.Overloads[i], i);
            }
        }

        private void WriteProperty(IPythonPropertyType prop) {
            var propType = prop.Type;
            var t = propType?.IsUnknown() == false ? propType.Name : "?";
            _sb.AppendLine($"{prop.FullyQualifiedName} -> {t}");
        }

        private void WriteOverload(string functionName, IPythonFunctionOverload o, int index) {
            var retVal = o.StaticReturnValue?.GetPythonType()?.Name ?? "?";
            var s = index >= 0 ? $"{functionName}.{index} -> {retVal}" : $"{functionName} -> {retVal}";
            _sb.AppendLine(s);
        }

        private void WriteInstance(string className, string memberName, IPythonInstance inst) {
            var type = inst.GetPythonType();
            var t = type?.IsUnknown() == false ? type.Name : "?";
            _sb.AppendLine($"{className}.{memberName} -> {t}");
        }
    }
}
