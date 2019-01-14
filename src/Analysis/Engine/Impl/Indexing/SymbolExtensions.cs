﻿// Python Tools for Visual Studio
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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PythonTools.Analysis.Indexing {
    internal static class SymbolExtensions {
        public static IEnumerable<FlatSymbol> Flatten(this IEnumerable<HierarchicalSymbol> docSyms, Uri uri, string parent = null, int? depthLimit = null) {
            foreach (var sym in docSyms ?? Enumerable.Empty<HierarchicalSymbol>()) {
                yield return new FlatSymbol(sym.Name, sym.Kind, uri, sym.SelectionRange, parent);

                if (depthLimit != null) {
                    if (depthLimit < 1) {
                        yield break;
                    }
                    depthLimit--;
                }

                foreach (var si in sym.Children.Flatten(uri, sym.Name, depthLimit)) {
                    yield return si;
                }
            }
        }
    }
}
