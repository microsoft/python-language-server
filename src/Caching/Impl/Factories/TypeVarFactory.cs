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
using Microsoft.Python.Analysis.Specializations.Typing.Types;
using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis.Caching.Factories {
    internal sealed class TypeVarFactory : FactoryBase<TypeVarModel, IPythonType> {
        public TypeVarFactory(IEnumerable<TypeVarModel> models, ModuleFactory mf)
            : base(models, mf) {
        }

        public override IPythonType CreateMember(TypeVarModel tvm, IPythonType declaringType)
            => new GenericTypeParameter(tvm.Name, ModuleFactory.Module,
                tvm.Constraints.Select(c => ModuleFactory.ConstructType(c)).ToArray(),
                tvm.Bound, tvm.Covariant, tvm.Contravariant, ModuleFactory.DefaultLocation);
    }
}
