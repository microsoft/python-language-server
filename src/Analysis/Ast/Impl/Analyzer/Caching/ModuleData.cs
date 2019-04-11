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
using Newtonsoft.Json;

namespace Microsoft.Python.Analysis.Analyzer.Caching {
    [Serializable]
    internal sealed class ModuleData : IModuleData {
        [JsonProperty]
        public string ModuleName { get; set; }
        [JsonProperty]
        public string ModulePath { get; set; }
        [JsonIgnore] // Not caching classes just yet.
        public Dictionary<string, ClassData> Classes { get; set; } = new Dictionary<string, ClassData>();
        [JsonProperty]
        public Dictionary<string, string> Functions { get; set; } = new Dictionary<string, string>();

        [JsonConstructor]
        public ModuleData() { }

        public ModuleData(IPythonModule module) {
            ModuleName = module.Name;
            ModulePath = module.FilePath;
        }


        IReadOnlyDictionary<string, IClassData> IModuleData.Classes => Classes.ToDictionary(k => k.Key, k => (IClassData)k.Value);
        IReadOnlyDictionary<string, string> IModuleData.Functions => Functions;

        public static ModuleData FromModule(IPythonModule module) {
            var guard = new HashSet<IMember>();
            var md = new ModuleData(module);

            foreach (var v in module.GlobalScope.Variables) {
                var t = v.Value.GetPythonType();
                if (t?.DeclaringModule != module || v.Name.StartsWith("__")) {
                    continue;
                }

                try {
                    guard.Add(t);
                    switch (t) {
                        case IPythonClassType cls:
                            md.Classes[cls.Name] = ClassData.FromClass(cls, guard);
                            break;
                        case IPythonFunctionType ft when ft.Name != "<lambda>":
                            MakeFunctionData(ft, md);
                            break;
                    }
                } finally {
                    guard.Remove(t);
                }
            }

            // Add all methods and properties to the top-level
            // functions list for faster retrieval
            AddInnerFunctionsToGlobalList(md.Classes, md, string.Empty);
            return md;
        }

        private static void MakeFunctionData(IPythonFunctionType ft, ModuleData md) {
            switch (ft.Overloads.Count) {
                case 0:
                    return;
                case 1:
                    md.Functions[ft.Name] = ft.Overloads[0].StaticReturnValue?.GetPythonType()?.Name;
                    return;
            }

            for (var i = 0; i < ft.Overloads.Count; i++) {
                md.Functions[$"{ft.Name}.{i}"] = ft.Overloads[0].StaticReturnValue?.GetPythonType()?.Name;
            }
        }

        private static void AddInnerFunctionsToGlobalList(IReadOnlyDictionary<string, ClassData> classes, ModuleData md, string prefix) {
            foreach (var c in classes) {
                var className = c.Key;
                var cls = c.Value;

                var nextPrefix = string.IsNullOrWhiteSpace(prefix) ? className : $"{prefix}.{className}";
                AddInnerFunctionsToGlobalList(cls.Classes, md, nextPrefix);

                foreach (var kvp in cls.Methods) {
                    md.Functions[$"{prefix}.{kvp.Key}"] = kvp.Value;
                }
                foreach (var kvp in cls.Properties) {
                    md.Functions[$"{prefix}.{kvp.Key}"] = kvp.Value;
                }
            }
        }
    }
}
