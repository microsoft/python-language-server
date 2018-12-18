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
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Types {
    public sealed class PythonTypeComparer : IEqualityComparer<IPythonType> {
        public static readonly PythonTypeComparer Instance = new PythonTypeComparer();

        public bool Equals(IPythonType x, IPythonType y) {
            if (x == null || y == null) {
                return x == null && y == null;
            }
            if (ReferenceEquals(x, y)) {
                return true;
            }
            if(x is IPythonUnionType utx && y is IPythonUnionType uty) {
                return utx.SetEquals(uty, Instance);
            }
            return x.TypeId == y.TypeId &&
                   x.Name == y.Name &&
                   x.IsBuiltin == y.IsBuiltin &&
                   x.DeclaringModule == y.DeclaringModule &&
                   x.Documentation == y.Documentation;
        }

        public int GetHashCode(IPythonType obj) {
            if (obj == null) {
                return 0;
            }
            return obj.TypeId.GetHashCode() ^
                   obj.Name?.GetHashCode() ?? 0 ^
                   obj.IsBuiltin.GetHashCode() ^
                   obj.DeclaringModule?.GetHashCode() ?? 0 ^
                   obj.Documentation?.GetHashCode() ?? 0;
        }
    }
}
