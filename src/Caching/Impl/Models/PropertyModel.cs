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
using Microsoft.Python.Analysis.Types;
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
// ReSharper disable MemberCanBePrivate.Global

namespace Microsoft.Python.Analysis.Caching.Models {
    [Serializable]
    internal sealed class PropertyModel : CallableModel {
        public string ReturnType { get; set; }
        public PropertyModel() { } // For de-serializer from JSON

        [NonSerialized] private PythonPropertyType _property;

        public PropertyModel(IPythonPropertyType prop) : base(prop) {
            ReturnType = prop.ReturnType.GetPersistentQualifiedName();
        }

        protected override IMember ReConstruct(ModuleFactory mf, IPythonType declaringType) {
            if (_property != null) {
                return _property;
            }
            _property = new PythonPropertyType(Name, new Location(mf.Module, IndexSpan.ToSpan()), declaringType, (Attributes & FunctionAttributes.Abstract) != 0);
            _property.SetDocumentation(Documentation);

            var o = new PythonFunctionOverload(Name, mf.DefaultLocation);
            o.SetDocumentation(Documentation);
            o.SetReturnValue(mf.ConstructMember(ReturnType), true);
            _property.AddOverload(o);

            return _property;
        }
    }
}
