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
using System.Collections.Generic;
using System.Linq;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Newtonsoft.Json;

namespace Microsoft.Python.Analysis.Analyzer.Caching {
    [Serializable]
    internal sealed class ClassData: IClassData {
        [JsonProperty]
        public string ClassName { get; set; }
        [JsonProperty]
        public Dictionary<string, string> Fields { get; set; } = new Dictionary<string, string>();
        [JsonProperty]
        public Dictionary<string, string> Methods { get; set; } = new Dictionary<string, string>();
        [JsonProperty]
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
        [JsonProperty]
        public Dictionary<string, ClassData> Classes { get; set; } = new Dictionary<string, ClassData>();

        IReadOnlyDictionary<string, string> IClassData.Fields => Fields;
        IReadOnlyDictionary<string, string> IClassData.Methods => Methods;
        IReadOnlyDictionary<string, string> IClassData.Properties  => Properties;
        IReadOnlyDictionary<string, IClassData> IClassData.Classes => Classes.ToDictionary(k => k.Key, k => (IClassData)k.Value);

        [JsonConstructor]
        public ClassData() { }

        public ClassData(string className) {
            ClassName = className;
        }

        public static ClassData FromClass(IPythonClassType cls, HashSet<IMember> guard) {
            var cd = new ClassData(cls.GetFullyQualifiedName());

            foreach (var name in cls.GetMemberNames()) {
                if (name.StartsWithOrdinal("__")) {
                    continue;
                }
                var m = cls.GetMember(name);
                var t = m.GetPythonType();
                if (t.DeclaringType != cls || guard.Contains(m) || guard.Contains(t)) {
                    continue;
                }

                try {
                    guard.Add(m);
                    switch (m) {
                        case IPythonClassType ct:
                            cd.Classes[name] = FromClass(ct, guard);
                            break;
                        case IPythonFunctionType ft when ft.Name != "<lambda>":
                            MakeFunctionData(ft, cd);
                            break;
                        case IPythonPropertyType prop:
                            cd.Properties[name] = prop.Type?.Name;
                            break;
                        case IPythonInstance inst:
                            cd.Fields[name] = inst.GetPythonType()?.Name;
                            break;
                    }
                } finally {
                    guard.Remove(m);
                }
            }
            return cd;
        }

        private static void MakeFunctionData(IPythonFunctionType ft, ClassData cd) {
            if (ft.Overloads.Count > 0) {
                cd.Methods[ft.Name] = ft.Overloads[0].StaticReturnValue?.GetPythonType()?.Name;
            }
        }
    }
}
