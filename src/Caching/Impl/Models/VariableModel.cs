﻿// Copyright(c) Microsoft Corporation
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

using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Caching.Models {
    internal sealed class VariableModel {
        public string Name { get; set; }
        public string Value { get; set; }

        public static VariableModel FromVariable(IVariable v) => new VariableModel {
            Name = v.Name,
            Value = v.Value.GetQualifiedName()
        };

        public static VariableModel FromInstance(string name, IPythonInstance inst) => new VariableModel {
            Name = name,
            Value = inst.GetQualifiedName()
        };

        public static VariableModel FromType(string name, IPythonType t) => new VariableModel {
            Name = name,
            Value = t.GetQualifiedName()
        };
    }
}