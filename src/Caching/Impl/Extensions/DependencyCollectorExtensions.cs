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
using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Analysis.Dependencies;
using Microsoft.Python.Core.Collections;

namespace Microsoft.Python.Analysis.Caching {
    internal static class DependencyCollectorExtensions {
        public static void AddImports(this DependencyCollector dc, IEnumerable<ImportModel> imports) {
            foreach (var imp in imports) {
                foreach (var dottedName in imp.ModuleNames) {
                    var importNames = ImmutableArray<string>.Empty;
                    foreach (var part in dottedName.NameParts) {
                        importNames = importNames.Add(part);
                        dc.AddImport(importNames, imp.ForceAbsolute);
                    }
                }
            }
        }

        public static void AddFromImports(this DependencyCollector dc, IEnumerable<FromImportModel> imports) {
            foreach (var imp in imports) {
                dc.AddFromImport(imp.RootNames, imp.MemberNames, imp.DotCount, imp.ForceAbsolute);
            }
        }
    }
}
