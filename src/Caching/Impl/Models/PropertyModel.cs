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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
// ReSharper disable MemberCanBePrivate.Global

namespace Microsoft.Python.Analysis.Caching.Models {
    [Serializable]
    internal sealed class PropertyModel : CallableModel {
        public string ReturnType { get; set; }
        public PropertyModel() { } // For de-serializer from JSON

        [NonSerialized] private PythonPropertyType _property;

        public PropertyModel(IPythonPropertyType prop, IServiceContainer services) : base(prop, services) {
            ReturnType = prop.ReturnType.GetPersistentQualifiedName(services);
        }

        protected override IMember DeclareMember(IPythonType declaringType) {
            Debug.Assert(_property == null);
            _property = new PythonPropertyType(Name, new Location(_mf.Module, IndexSpan.ToSpan()), Documentation, declaringType, (Attributes & FunctionAttributes.Abstract) != 0);
            _property.SetDocumentation(Documentation);
            return _property;
        }

        protected override void FinalizeMember() {
            var o = new PythonFunctionOverload(_property, _mf.DefaultLocation);
            o.SetDocumentation(Documentation);
            o.SetReturnValue(_mf.ConstructMember(ReturnType), true);
            _property.AddOverload(o);
        }
    }
}
