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
using System.Linq;
using Microsoft.Python.Analysis.Types;
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
// ReSharper disable MemberCanBePrivate.Global

namespace Microsoft.Python.Analysis.Caching.Models {
    [Serializable]
    [DebuggerDisplay("f:{" + nameof(Name) + "}")]
    internal sealed class FunctionModel : CallableModel {
        public OverloadModel[] Overloads { get; set; }

        public FunctionModel() { } // For de-serializer from JSON

        public FunctionModel(IPythonFunctionType func) : base(func) {
            Overloads = func.Overloads.Select(FromOverload).ToArray();
        }

        protected override IMember ReConstruct(ModuleFactory mf, IPythonType declaringType) {
            var ft = new PythonFunctionType(Name, new Location(mf.Module, IndexSpan.ToSpan()), declaringType, Documentation);

            // Create inner functions and classes first since function
            // may be returning one of them.
            foreach (var model in Functions) {
                var f = model.Construct(mf, ft);
                ft.AddMember(Name, f, overwrite: true);
            }

            foreach (var cm in Classes) {
                var c = cm.Construct(mf, ft);
                ft.AddMember(cm.Name, c, overwrite: true);
            }

            foreach (var om in Overloads) {
                var o = new PythonFunctionOverload(Name, new Location(mf.Module, IndexSpan.ToSpan()));
                o.SetDocumentation(Documentation);
                o.SetReturnValue(mf.ConstructMember(om.ReturnType), true);
                o.SetParameters(om.Parameters.Select(p => ConstructParameter(mf, p)).ToArray());
                ft.AddOverload(o);
            }

            return ft;
        }
        private IParameterInfo ConstructParameter(ModuleFactory mf, ParameterModel pm)
            => new ParameterInfo(pm.Name, mf.ConstructType(pm.Type), pm.Kind, mf.ConstructMember(pm.DefaultValue));

        private static OverloadModel FromOverload(IPythonFunctionOverload o)
            => new OverloadModel {
                Parameters = o.Parameters.Select(p => new ParameterModel {
                    Name = p.Name,
                    Type = p.Type.GetPersistentQualifiedName(),
                    Kind = p.Kind,
                    DefaultValue = p.DefaultValue.GetPersistentQualifiedName(),
                }).ToArray(),
                ReturnType = o.StaticReturnValue.GetPersistentQualifiedName()
            };
    }
}
