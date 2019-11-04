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
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Caching.Lazy {
    internal sealed class PythonLazyPropertyType: PythonTypeWrapper, IPythonPropertyType {
        private readonly PythonPropertyType _innerProperty;
        private FunctionModel _model;

        public PythonLazyPropertyType(PythonPropertyType innerProperty) {
            _innerProperty = innerProperty;
        }

        public IPythonType DeclaringType => _innerProperty.DeclaringType;
        public FunctionDefinition FunctionDefinition => null;
        public string Description => _innerProperty.Description;
        public bool IsReadOnly => _innerProperty.IsReadOnly;
        public IMember ReturnType {
            get {
                EnsureContent();
                return _innerProperty.ReturnType;
            }
        }
        private void EnsureContent() {
            _model?.CreateContent();
            _model = null;
        }
    }
}
