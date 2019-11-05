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
using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Analysis.Specializations.Typing.Types;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Caching.Lazy {
    internal static class MemberFactory {
        public static IMember CreateMember(MemberModel model, ModuleFactory mf, IGlobalScope gs, IPythonType declaringType) {
            switch (model) {
                case ClassModel cm:
                    return new PythonLazyClassType(cm, mf, gs, declaringType);
                case FunctionModel fm:
                    return new PythonLazyFunctionType(fm, mf, gs, declaringType);
                case PropertyModel pm:
                    return new PythonLazyPropertyType(pm, mf, gs, declaringType);

                case NamedTupleModel ntm:
                    var itemTypes = ntm.ItemTypes.Select(mf.ConstructType).ToArray();
                    return new NamedTupleType(ntm.Name, ntm.ItemNames, itemTypes, mf.Module, ntm.IndexSpan.ToSpan());

                case TypeVarModel tvm:
                    return new GenericTypeParameter(tvm.Name, mf.Module,
                        tvm.Constraints.Select(mf.ConstructType).ToArray(),
                    mf.ConstructType(tvm.Bound), tvm.Covariant, tvm.Contravariant, default);

                case VariableModel vm:
                    var m = mf.ConstructMember(vm.Value) ?? mf.Module.Interpreter.UnknownType;
                    return new Variable(vm.Name, m, VariableSource.Declaration, new Location(mf.Module, vm.IndexSpan?.ToSpan() ?? default));

            }
            Debug.Fail("Unsupported model type.");
            return null;
        }
    }
}
