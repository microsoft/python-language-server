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
using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis.Caching.Models {
    [DebuggerDisplay("f:{Name}")]
    internal sealed class FunctionModel: MemberModel {
        public string Documentation { get; set; }
        public OverloadModel[] Overloads { get; set; }
        public FunctionAttributes Attributes { get; set; }
        public string[] Classes { get; set; }
        public string[] Functions { get; set; }

        public static FunctionModel FromType(IPythonFunctionType ft) {
            return new FunctionModel {
                Name = ft.Name,
                Documentation = ft.Documentation,
                Overloads = ft.Overloads.Select(FromOverload).ToArray()
                // TODO: attributes, inner functions and inner classes.
            };
        }

        private static OverloadModel FromOverload(IPythonFunctionOverload o) {
            return new OverloadModel {
                Parameters = o.Parameters.Select(p => new ParameterModel {
                    Name = p.Name,
                    Type = p.Type.GetQualifiedName(),
                    Kind = p.Kind,
                    DefaultValue = p.DefaultValue.GetQualifiedName(),
                }).ToArray(),
                ReturnType = o.StaticReturnValue.GetQualifiedName()
            };
        }
    }
}
