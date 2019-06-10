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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Caching {
    internal static class TypeNames {
        public static string GetQualifiedName(this IMember m) {
            var t = m.GetPythonType();
            if (t.IsUnknown()) {
                return null;
            }

            var moduleId = t.DeclaringModule.UniqueId;
            switch (m) {
                case IPythonModule mod:
                    return mod.UniqueId;
                case IPythonInstance _:
                    return $"i:{GetQualifiedName(t)}";
                case IPythonClassMember cm when cm.DeclaringType != null:
                    return $"{moduleId}.{cm.GetQualifiedName()}";
                case IPythonType pt:
                    return $"{moduleId}.{pt.Name}";
                case null:
                    break;
            }
            return null;
        }

        private static string GetQualifiedName(this IPythonClassMember cm) {
            var s = new Stack<string>();
            s.Push(cm.Name);
            for (var p = cm.DeclaringType as IPythonClassMember; p != null; p = p.DeclaringType as IPythonClassMember) {
                s.Push(p.Name);
            }
            return string.Join(".", s);
        }
    }
}
