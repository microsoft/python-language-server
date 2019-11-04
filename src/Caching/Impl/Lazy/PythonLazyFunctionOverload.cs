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
using System.Collections.Generic;
using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Caching.Lazy {
    internal sealed class PythonLazyFunctionOverload: IPythonFunctionOverload {
        private OverloadModel _model;

        public PythonLazyFunctionOverload(OverloadModel model) {
            _model = model;
        }

        public FunctionDefinition FunctionDefinition { get; }
        public IPythonClassMember ClassMember { get; }
        public string Name { get; }
        public string Documentation { get; }
        public IReadOnlyList<IParameterInfo> Parameters { get; }
        public IMember Call(IArgumentSet args, IPythonType self) {
            throw new NotImplementedException();
        }

        public string GetReturnDocumentation(IPythonType self = null) {
            throw new NotImplementedException();
        }

        public IMember StaticReturnValue { get; }
    }
}
