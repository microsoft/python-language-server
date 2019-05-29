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
    public struct Location {
        public Location(IPythonModule module) : this(module, default) { }

        public Location(IPythonModule module, IndexSpan indexSpan) {
            Module = module;
            IndexSpan = indexSpan;
        }

        public IPythonModule Module { get; }
        public IndexSpan IndexSpan { get; }

        public LocationInfo LocationInfo {
            get {
                var ast = Module.GetAst();
                if (ast != null && !string.IsNullOrEmpty(Module?.FilePath) && Module?.Uri != null) {
                    return new LocationInfo(Module.FilePath, Module.Uri, IndexSpan.ToSourceSpan(ast));
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
