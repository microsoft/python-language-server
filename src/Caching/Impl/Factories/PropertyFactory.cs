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

using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis.Caching.Factories {
    internal sealed class PropertyFactory {
        private readonly ModuleFactory _mf;

        public PropertyFactory(ModuleFactory mf) {
            _mf = mf;
        }

        public IPythonPropertyType Construct(PropertyModel pm, IPythonClassType cls) {
            var prop = new PythonPropertyType(pm.Name, _mf.DefaultLocation, cls, (pm.Attributes & FunctionAttributes.Abstract) != 0);
            prop.SetDocumentation(pm.Documentation);
            var o = new PythonFunctionOverload(pm.Name, _mf.DefaultLocation);
            o.SetDocumentation(pm.Documentation); // TODO: own documentation?
            o.SetReturnValue(_mf.ConstructMember(pm.ReturnType), true);
            prop.AddOverload(o);
            return prop;
        }
    }
}
