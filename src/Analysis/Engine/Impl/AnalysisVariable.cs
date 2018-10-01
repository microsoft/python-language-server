// Python Tools for Visual Studio
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

namespace Microsoft.PythonTools.Analysis {
    class AnalysisVariable : IAnalysisVariable {
        public AnalysisVariable(IVariableDefinition variable, VariableType type, ILocationInfo location, int? version = null) {
            Variable = variable;
            Location = location;
            Type = type;
            Version = version;
        }

        #region IAnalysisVariable Members
        public ILocationInfo Location { get; }
        public VariableType Type { get; }
        public int? Version { get; }
        public IVariableDefinition Variable { get; }
        #endregion

        public override bool Equals(object obj) {
            var other = obj as AnalysisVariable;
            if (other != null) {
                return LocationInfo.FullComparer.Equals(Location, other.Location) &&
                       Type.Equals(other.Type) &&
                       Version == other.Version;
            }
            return false;
        }

        public override int GetHashCode() => Type.GetHashCode() ^ Location?.GetHashCode() ?? 0;
    }
}
