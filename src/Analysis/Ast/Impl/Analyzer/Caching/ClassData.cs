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

namespace Microsoft.Python.Analysis.Analyzer.Caching {
    internal sealed class ClassData : IClassData {
        IReadOnlyDictionary<string, string> IClassData.Fields => Fields;
        IReadOnlyDictionary<string, string> IClassData.Methods => Methods;
        IReadOnlyDictionary<string, string> IClassData.Properties => Properties;
        IReadOnlyDictionary<string, IClassData> IClassData.Classes => Classes;

        public Dictionary<string, string> Fields { get; } = new Dictionary<string, string>();
        public Dictionary<string, string> Methods { get; } = new Dictionary<string, string>();
        public Dictionary<string, string> Properties { get; } = new Dictionary<string, string>();
        public Dictionary<string, IClassData> Classes { get; } = new Dictionary<string, IClassData>();
    }
}
