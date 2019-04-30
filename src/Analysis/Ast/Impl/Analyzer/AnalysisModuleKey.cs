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
using System.Diagnostics;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Analyzer {
    [DebuggerDisplay("{Name} : {FilePath}")]
    internal struct AnalysisModuleKey : IEquatable<AnalysisModuleKey> {
        public string Name { get; }
        public string FilePath { get; }
        public bool IsTypeshed { get; }

        private readonly int _hashCode;

        public AnalysisModuleKey(IPythonModule module) {
            Name = module.Name;
            FilePath = module.ModuleType == ModuleType.CompiledBuiltin ? null : module.FilePath;
            IsTypeshed = module is StubPythonModule stub && stub.IsTypeshed;
            _hashCode = CreateHashCode(Name, FilePath, IsTypeshed);
        }

        public AnalysisModuleKey(string name, string filePath, bool isTypeshed) {
            Name = name;
            FilePath = filePath;
            IsTypeshed = isTypeshed;
            _hashCode = CreateHashCode(Name, FilePath, IsTypeshed);
        }

        public bool Equals(AnalysisModuleKey other)
            => Name.EqualsOrdinal(other.Name) && FilePath.PathEquals(other.FilePath) && IsTypeshed == other.IsTypeshed;

        public override bool Equals(object obj) => obj is AnalysisModuleKey other && Equals(other);

        public override int GetHashCode() => _hashCode;

        private static int CreateHashCode(string name, string filePath, bool isTypeshed) {
            unchecked {
                var hashCode = (name != null ? name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (filePath != null ? filePath.GetPathHashCode() : 0);
                hashCode = (hashCode * 397) ^ isTypeshed.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(AnalysisModuleKey left, AnalysisModuleKey right) => left.Equals(right);

        public static bool operator !=(AnalysisModuleKey left, AnalysisModuleKey right) => !left.Equals(right);

        public void Deconstruct(out string moduleName, out string filePath, out bool isTypeshed) {
            moduleName = Name;
            filePath = FilePath;
            isTypeshed = IsTypeshed;
        }

        public override string ToString() => $"{Name}({FilePath})";
    }
}
