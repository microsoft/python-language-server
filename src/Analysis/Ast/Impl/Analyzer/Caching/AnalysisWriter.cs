﻿// Copyright(c) Microsoft Corporation
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

using System.IO;
using Microsoft.Python.Analysis.Types;
using Newtonsoft.Json;

namespace Microsoft.Python.Analysis.Analyzer.Caching {
    internal sealed class AnalysisWriter {
        private readonly IPythonModule _module;

        public AnalysisWriter(IPythonModule module) {
            _module = module;
        }

        public string SerializeModuleData() {
            var md = ModuleData.FromModule(_module);
            if (md.Classes.Count == 0 && md.Functions.Count == 0) {
                return null;
            }

            using (var sw = new StringWriter())
            using (var jw = new JsonTextWriter(sw)) {
#if DEBUG
                jw.Formatting = Formatting.Indented;
                jw.Indentation = 2;
                jw.IndentChar = ' ';
#endif
                JsonSerializer.Create().Serialize(jw, md);
                return sw.GetStringBuilder().ToString();
            }
        }
    }
}
