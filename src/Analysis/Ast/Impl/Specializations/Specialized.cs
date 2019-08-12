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

using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis.Specializations {
    internal static class Specialized {
        public static IPythonPropertyType Property(string name, IPythonModule declaringModule, IPythonType declaringType, IMember returnValue) {
            var location = new Location(declaringModule);
            var prop = new PythonPropertyType(name, location, declaringType);
            var o = new PythonFunctionOverload(prop.Name, location);
            o.AddReturnValue(returnValue);
            prop.AddOverload(o);
            return prop;
        }

        public static IPythonFunctionType Function(string name, IPythonModule declaringModule, string documentation, IMember returnValue) {
            var location = new Location(declaringModule);
            var prop = PythonFunctionType.Specialize(name, declaringModule, documentation);
            var o = new PythonFunctionOverload(prop.Name, location);
            o.AddReturnValue(returnValue);
            prop.AddOverload(o);
            return prop;
        }
    }
}
