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
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Caching.Lazy {
    internal sealed class PythonLazyFunctionType : PythonLazyType<FunctionModel>, IPythonFunctionType {
        private readonly PythonFunctionType _function;

        public PythonLazyFunctionType(FunctionModel model, ModuleFactory mf, IGlobalScope gs, IPythonType declaringType)
        : base(model, mf, gs, declaringType) {
            var location = new Location(mf.Module, model.IndexSpan.ToSpan());
            _function = new PythonFunctionType(Model.Name, location, declaringType, model.Documentation);

            // TODO: restore signature string so hover (tooltip) documentation won't have to restore the function.
            // parameters and return type just to look at them.
            foreach (var om in Model.Overloads) {
                var o = new PythonLazyOverload(om, mf, _function);
                _function.AddOverload(o);
            }
            SetInnerType(_function);
        }

        #region IPythonFunctionType
        public FunctionDefinition FunctionDefinition => null;
        public bool IsClassMethod => _function.IsClassMethod;
        public bool IsStatic => _function.IsStatic;
        public bool IsOverload => _function.IsStatic;
        public bool IsStub => _function.IsStatic;
        public bool IsUnbound => _function.IsStatic;
        public IReadOnlyList<IPythonFunctionOverload> Overloads => _function.Overloads;
        #endregion

        protected override void EnsureContent() {
            if (Model == null) {
                return;
            }
            var innerTypes = Model.Classes.Concat<MemberModel>(Model.Functions).ToArray();
            foreach (var model in innerTypes) {
                _function.AddMember(Name, MemberFactory.CreateMember(model, ModuleFactory, GlobalScope, _function), overwrite: true);
            }
            ReleaseModel();
        }
    }
}
