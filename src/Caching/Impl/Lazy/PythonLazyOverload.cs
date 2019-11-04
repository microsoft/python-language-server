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
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Caching.Lazy {
    internal sealed class PythonLazyOverload: IPythonFunctionOverload {
        private readonly PythonFunctionOverload _overload;
        private ModuleFactory _mf;
        private OverloadModel _model;

        public PythonLazyOverload(OverloadModel model, ModuleFactory mf, IPythonClassMember cm) {
            _model = model;
            _mf = mf;
            
            ClassMember = cm;
            Documentation = model.Documentation;

            _overload = new PythonFunctionOverload(cm, new Location(mf.Module, default));
            _overload.SetDocumentation(cm.Documentation);
        }

        public FunctionDefinition FunctionDefinition => null;
        public IPythonClassMember ClassMember { get; }
        public string Name => ClassMember.Name;
        public string Documentation { get; }

        public IReadOnlyList<IParameterInfo> Parameters {
            get {
                EnsureContent();
                return _overload.Parameters;
            }
        }

        public IMember Call(IArgumentSet args, IPythonType self) {
            EnsureContent();
            return _overload.Call(args, self);
        }

        public string GetReturnDocumentation(IPythonType self = null) {
            EnsureContent();
            return _overload.GetReturnDocumentation(self);
        }

        public IMember StaticReturnValue {
            get {
                EnsureContent();
                return _overload.StaticReturnValue;
            }
        }

        private void EnsureContent() {
            if (_model == null) {
                return;
            }
            _overload.SetParameters(_model.Parameters.Select(p => ConstructParameter(_mf, p)).ToArray());
            _overload.SetReturnValue(_mf.ConstructMember(_model.ReturnType), true);

            _model = null;
            _mf = null;
        }
        private IParameterInfo ConstructParameter(ModuleFactory mf, ParameterModel pm)
            => new ParameterInfo(pm.Name, mf.ConstructType(pm.Type), pm.Kind, mf.ConstructMember(pm.DefaultValue));
    }
}
