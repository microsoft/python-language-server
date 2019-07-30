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
using System.Linq;
using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis.Caching.Factories {
    internal sealed class FunctionFactory: FactoryBase<FunctionModel, IPythonFunctionType> {
        public FunctionFactory(IEnumerable<FunctionModel> classes, ModuleFactory mf)
            : base(classes, mf) {
        }

        protected override IPythonFunctionType CreateMember(FunctionModel fm, IPythonType declaringType) {
            var ft = new PythonFunctionType(fm.Name, new Location(ModuleFactory.Module, fm.IndexSpan.ToSpan()), declaringType, fm.Documentation);

            foreach (var om in fm.Overloads) {
                var o = new PythonFunctionOverload(fm.Name, new Location(ModuleFactory.Module, fm.IndexSpan.ToSpan()));
                o.SetDocumentation(fm.Documentation);
                o.SetReturnValue(ModuleFactory.ConstructMember(om.ReturnType), true);
                o.SetParameters(om.Parameters.Select(ConstructParameter).ToArray());
                ft.AddOverload(o);
            }

            foreach(var model in fm.Functions) {
                var f = CreateMember(model, ft);
                if (f != null) {
                    ft.AddMember(f.Name, f, overwrite: true);
                }
            }

            foreach (var model in fm.Classes) {
                var c = ModuleFactory.ClassFactory.Construct(model, ft);
                if (c != null) {
                    ft.AddMember(c.Name, c, overwrite: true);
                }
            }

            return ft;
        }

        private IParameterInfo ConstructParameter(ParameterModel pm)
            => new ParameterInfo(pm.Name, ModuleFactory.ConstructType(pm.Type), pm.Kind, ModuleFactory.ConstructMember(pm.DefaultValue));
    }
}
