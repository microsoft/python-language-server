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
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
// ReSharper disable MemberCanBePrivate.Global

namespace Microsoft.Python.Analysis.Caching.Models {
    [Serializable]
    [DebuggerDisplay("f:{" + nameof(Name) + "}")]
    internal sealed class FunctionModel : CallableModel {
        public OverloadModel[] Overloads { get; set; }
        public FunctionModel() { } // For de-serializer from JSON

        [NonSerialized] private PythonFunctionType _function;

        public FunctionModel(IPythonFunctionType func, IServiceContainer services) : base(func, services) {
            Overloads = func.Overloads.Select(s => FromOverload(s, services)).ToArray();
        }

        protected override IMember DeclareMember(IPythonType declaringType) {
            Debug.Assert(_function == null);
            _function = new PythonFunctionType(Name, new Location(_mf.Module, IndexSpan.ToSpan()), declaringType, Documentation);
            // TODO: restore signature string so hover does not need to restore function
            // parameters and return type just to look at them.
            for (var i = 0; i < Overloads.Length; i++) {
                var o = new PythonFunctionOverload(_function, new Location(_mf.Module, IndexSpan.ToSpan()));
                o.SetDocumentation(Documentation);
                _function.AddOverload(o);
            }
            return _function;
        }

        protected override void FinalizeMember() {
            // DeclareMember inner functions and classes first since function may be returning one of them.
            var innerTypes = Classes.Concat<MemberModel>(Functions).ToArray();
            foreach (var model in innerTypes) {
                _function.AddMember(Name, model.Declare(_mf, _function, _gs), overwrite: true);
            }
            foreach (var model in innerTypes) {
                model.Finalize();
            }

            for (var i = 0; i < Overloads.Length; i++) {
                var om = Overloads[i];
                var o = (PythonFunctionOverload)_function.Overloads[i];
                o.SetReturnValue(_mf.ConstructMember(om.ReturnType), true);
                o.SetParameters(om.Parameters.Select(p => ConstructParameter(_mf, p)).ToArray());
            }
        }

        private IParameterInfo ConstructParameter(ModuleFactory mf, ParameterModel pm)
            => new ParameterInfo(pm.Name, mf.ConstructType(pm.Type), pm.Kind, mf.ConstructMember(pm.DefaultValue));

        private static OverloadModel FromOverload(IPythonFunctionOverload o, IServiceContainer services)
            => new OverloadModel {
                Parameters = o.Parameters.Select(p => new ParameterModel {
                    Name = p.Name,
                    Type = p.Type.GetPersistentQualifiedName(services),
                    Kind = p.Kind,
                    DefaultValue = p.DefaultValue.GetPersistentQualifiedName(services),
                }).ToArray(),
                ReturnType = o.StaticReturnValue.GetPersistentQualifiedName(services)
            };
    }
}
