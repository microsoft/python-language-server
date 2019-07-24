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
using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Caching.Factories {
    internal sealed class VariableFactory : FactoryBase<VariableModel, IVariable> {
        public VariableFactory(IEnumerable<VariableModel> models, ModuleFactory mf)
            : base(models, mf) {
        }

        protected override IVariable CreateMember(VariableModel vm, IPythonType declaringType) {
            var m = ModuleFactory.ConstructMember(vm.Value) ?? ModuleFactory.Module.Interpreter.UnknownType;
            return new Variable(vm.Name, m, VariableSource.Declaration, new Location(ModuleFactory.Module, vm.IndexSpan?.ToSpan() ?? default));
        }
    }
}
