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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Caching.Models {
    [DebuggerDisplay("v:{Name} = {Value}")]
    internal sealed class VariableModel: MemberModel {
        public string Value { get; set; }

        public static VariableModel FromVariable(IVariable v) => new VariableModel {
            Id = v.Name.GetStableHash(),
            Name = v.Name,
            IndexSpan = v.Location.IndexSpan.ToModel(),
            Value = v.Value.GetPersistentQualifiedName()
        };

        public static VariableModel FromInstance(string name, IPythonInstance inst) => new VariableModel {
            Id = name.GetStableHash(),
            Name = name,
            Value = inst.GetPersistentQualifiedName()
        };

        public static VariableModel FromType(string name, IPythonType t) => new VariableModel {
            Id = name.GetStableHash(),
            Name = name,
            IndexSpan = t.Location.IndexSpan.ToModel(),
            Value = t.GetPersistentQualifiedName()
        };
    }
}
