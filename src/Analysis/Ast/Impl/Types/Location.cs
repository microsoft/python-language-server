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

using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Types {
    public readonly struct Location {
        public Location(IPythonModule module, IndexSpan indexSpan = default) {
            Module = module;
            IndexSpan = indexSpan;
        }

        public void Deconstruct(out IPythonModule module, out IndexSpan indexSpan) => (module, indexSpan) = (Module, IndexSpan);

        public IPythonModule Module { get; }
        public IndexSpan IndexSpan { get; }

        public LocationInfo LocationInfo {
            get {
                if (Module is ILocationConverter lc && !string.IsNullOrEmpty(Module?.FilePath) && Module?.Uri != null) {
                    return new LocationInfo(Module.FilePath, Module.Uri, IndexSpan.ToSourceSpan(lc));
                }
                return LocationInfo.Empty;
            }
        }

        public bool IsValid => Module != null && IndexSpan != default;

        public override bool Equals(object obj)
            => obj is Location other && other.Module == Module && other.IndexSpan == IndexSpan;

        public override int GetHashCode() => (IndexSpan.GetHashCode() * 397) ^ Module?.GetHashCode() ?? 0;
    }
}
