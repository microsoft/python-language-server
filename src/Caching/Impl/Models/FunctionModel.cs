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
using System.Diagnostics;
using System.Linq;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Utilities;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Caching.Models {
    [DebuggerDisplay("f:{Name}")]
    internal sealed class FunctionModel : MemberModel {
        public string Documentation { get; set; }
        public OverloadModel[] Overloads { get; set; }
        public FunctionAttributes Attributes { get; set; }
        public ClassModel[] Classes { get; set; }
        public FunctionModel[] Functions { get; set; }

        private readonly ReentrancyGuard<IMember> _processing = new ReentrancyGuard<IMember>();

        public static FunctionModel FromType(IPythonFunctionType ft) => new FunctionModel(ft);

        private static OverloadModel FromOverload(IPythonFunctionOverload o) {
            return new OverloadModel {
                Parameters = o.Parameters.Select(p => new ParameterModel {
                    Name = p.Name,
                    Type = p.Type.GetPersistentQualifiedName(),
                    Kind = p.Kind,
                    DefaultValue = p.DefaultValue.GetPersistentQualifiedName(),
                }).ToArray(),
                ReturnType = o.StaticReturnValue.GetPersistentQualifiedName()
            };
        }

        public FunctionModel() { } // For de-serializer from JSON

        private FunctionModel(IPythonFunctionType func) {
            var functions = new List<FunctionModel>();
            var classes = new List<ClassModel>();

            foreach (var name in func.GetMemberNames()) {
                var m = func.GetMember(name);

                // Only take members from this class, skip members from bases.
                using (_processing.Push(m, out var reentered)) {
                    if (reentered) {
                        continue;
                    }
                    switch (m) {
                        case IPythonFunctionType ft when ft.IsLambda():
                            break;
                        case IPythonFunctionType ft:
                            functions.Add(FromType(ft));
                            break;
                        case IPythonClassType cls:
                            classes.Add(ClassModel.FromType(cls));
                            break;
                    }
                }
            }

            Id = func.Name.GetStableHash();
            Name = func.Name;
            IndexSpan = func.Location.IndexSpan.ToModel();
            Documentation = func.Documentation;
            Overloads = func.Overloads.Select(FromOverload).ToArray();
            Classes = classes.ToArray();
            Functions = functions.ToArray();
        }
    }
}
