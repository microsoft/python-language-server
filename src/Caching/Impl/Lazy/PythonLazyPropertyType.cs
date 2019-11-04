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
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Caching.Lazy {
    internal sealed class PythonLazyPropertyType : PythonLazyType<PropertyModel>, IPythonPropertyType {
        private readonly PythonPropertyType _property;

        public PythonLazyPropertyType(PropertyModel model, ModuleFactory mf, IGlobalScope gs, IPythonType declaringType)
            : base(model, mf, gs, declaringType) {

            var location = new Location(mf.Module, model.IndexSpan.ToSpan());
            _property = new PythonPropertyType(Model.Name, location, Model.Documentation, declaringType,
                Model.Attributes.HasFlag(FunctionAttributes.Abstract));

            // parameters and return type just to look at them.
            var o = new PythonFunctionOverload(_property, location);
            o.SetDocumentation(Documentation);
            _property.AddOverload(o);

            IsReadOnly = model.IsReadOnly;
            SetInnerType(_property);
        }

        public FunctionDefinition FunctionDefinition => null;
        public bool IsReadOnly { get; }

        public IMember ReturnType {
            get {
                EnsureContent();
                return _property.ReturnType;
            }
        }

        protected override void EnsureContent() {
            _property.Getter.SetReturnValue(ModuleFactory.ConstructMember(Model.ReturnType), true);
            ReleaseModel();
        }
    }
}
