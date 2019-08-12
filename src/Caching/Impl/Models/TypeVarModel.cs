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

using System.Diagnostics;
using System.Linq;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Caching.Models {
    [DebuggerDisplay("TypeVar:{Name}")]
    internal sealed class TypeVarModel: MemberModel {
        public string[] Constraints { get; set; }

        public static TypeVarModel FromGeneric(IVariable v) {
            var g = (IGenericTypeParameter)v.Value;
            return new TypeVarModel {
                Id = g.Name.GetStableHash(),
                Name = g.Name,
                Constraints = g.Constraints.Select(c => c.GetPersistentQualifiedName()).ToArray()
            };
        }
    }
}
