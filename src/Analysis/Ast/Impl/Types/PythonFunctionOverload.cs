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
using System.Linq;
using Microsoft.Python.Analysis.Analyzer;

namespace Microsoft.Python.Analysis.Types {
    internal sealed class PythonFunctionOverload : IPythonFunctionOverload, ILocatedMember {
        private IPythonType _returnType;

        public PythonFunctionOverload(
            string name,
            IEnumerable<IParameterInfo> parameters,
            LocationInfo loc,
            string returnDocumentation = null
        ) {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Parameters = parameters?.ToArray() ?? throw new ArgumentNullException(nameof(parameters));
            Location = loc ?? LocationInfo.Empty;
            ReturnDocumentation = returnDocumentation;
        }

        internal void SetDocumentation(string doc) {
            if (Documentation != null) {
                throw new InvalidOperationException("cannot set Documentation twice");
            }
            Documentation = doc;
        }

        internal void AddReturnType(IPythonType type) 
            => _returnType = _returnType == null ? type : PythonUnion.Combine(_returnType, type);

        public string Name { get; }
        public string Documentation { get; private set; }
        public string ReturnDocumentation { get; }
        public IReadOnlyList<IParameterInfo> Parameters { get; }
        public LocationInfo Location { get; }

        public IPythonType GetReturnType(IReadOnlyList<IPythonType> args) {
            if (_returnType is IPythonCallableArgumentType cat) {
                return cat.ParameterIndex < args.Count ? args[cat.ParameterIndex] : _returnType;
            }
            return _returnType;
        }
    }
}
