// Python Tools for Visual Studio
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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonFunctionOverload : IPythonFunctionOverload, ILocatedMember {
        private readonly string _name;
        private readonly AstScope _scope;
        private readonly IReadOnlyList<IParameterInfo> _parameters;
        private readonly List<IMember> _lazyReturnTypes = new List<IMember>();
        private readonly object _lock = new object();
        private IPythonType[] _returnTypes;
        private bool _changed = true;

        public AstPythonFunctionOverload(
            AstScope scope,
            string name,
            IEnumerable<IParameterInfo> parameters,
            ILocationInfo loc
        ) {
            _name = name;
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
            _parameters = parameters?.ToArray() ?? Array.Empty<IParameterInfo>();
            Locations = loc != null ? new[] { loc } : Array.Empty<ILocationInfo>();
        }

        internal void SetDocumentation(string doc) {
            if (Documentation != null) {
                throw new InvalidOperationException("cannot set Documentation twice");
            }
            Documentation = doc;
        }

        internal void AddReturnTypes(IEnumerable<IMember> types) {
            lock (_lock) {
                _changed = true;
                _lazyReturnTypes.AddRange(types);
            }
        }

        internal void AddReturnType(IMember type) {
            lock (_lock) {
                _changed = true;
                _lazyReturnTypes.Add(type);
            }
         }

        public string Documentation { get; private set; }
        public string ReturnDocumentation { get; }
        public IParameterInfo[] GetParameters() => _parameters.ToArray();

        public IReadOnlyList<IPythonType> ReturnType {
            get {
                lock (_lock) {
                    if (_changed) {
                        _returnTypes = _lazyReturnTypes.SelectMany(t => _scope.GetTypesFromValue(t.ResolveType())).ToArray();
                        _changed = false;
                    }
                    return _returnTypes;
                }
            }
        }

        public IEnumerable<ILocationInfo> Locations { get; }
        public PythonMemberType MemberType => PythonMemberType.Function;
        public string Name => string.Empty;
    }
}
