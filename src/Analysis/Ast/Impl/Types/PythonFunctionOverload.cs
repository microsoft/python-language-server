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
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Types {
    internal sealed class PythonFunctionOverload : IPythonFunctionOverload, ILocatedMember {
        private readonly Func<string, LocationInfo> _locationProvider;

        // Allow dynamic function specialization, such as defining return types for builtin
        // functions that are impossible to scrape and that are missing from stubs.
        private Func<IReadOnlyList<IMember>, IMember> _returnValueProvider;

        // Return value can be an instance or a type info. Consider type(C()) returning
        // type info of C vs. return C() that returns an instance of C.
        private Func<string, string> _documentationProvider;

        private IMember _returnValue;
        private bool _fromAnnotation;

        public PythonFunctionOverload(string name, IEnumerable<IParameterInfo> parameters,
            LocationInfo location, string returnDocumentation = null
        ) : this(name, parameters, _ => location ?? LocationInfo.Empty, returnDocumentation) { }

        public PythonFunctionOverload(FunctionDefinition fd, IEnumerable<IParameterInfo> parameters,
            LocationInfo location, string returnDocumentation = null
        ) : this(fd.Name, parameters, _ => location, returnDocumentation) {
            FunctionDefinition = fd;
        }

        public PythonFunctionOverload(string name, IEnumerable<IParameterInfo> parameters,
            Func<string, LocationInfo> locationProvider, string returnDocumentation = null
        ) {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Parameters = parameters?.ToArray() ?? throw new ArgumentNullException(nameof(parameters));
            _locationProvider = locationProvider;
            ReturnDocumentation = returnDocumentation;
        }

        internal void SetDocumentationProvider(Func<string, string> documentationProvider) {
            if (_documentationProvider != null) {
                throw new InvalidOperationException("cannot set documentation provider twice");
            }
            _documentationProvider = documentationProvider;
        }

        internal void AddReturnValue(IMember value) {
            if (_returnValue.IsUnknown()) {
                SetReturnValue(value, false);
                return;
            }
            // If return value is set from annotation, it should not be changing.
            if (!_fromAnnotation) {
                var type = PythonUnionType.Combine(_returnValue.GetPythonType(), value.GetPythonType());
                // Track instance vs type info.
                _returnValue = value is IPythonInstance ? new PythonInstance(type) : (IMember)type;
            }
        }

        internal void SetReturnValue(IMember value, bool fromAnnotation) {
            _returnValue = value;
            _fromAnnotation = fromAnnotation;
        }

        internal void SetReturnValueProvider(Func<IReadOnlyList<IMember>, IMember> provider)
            => _returnValueProvider = provider;

        #region IPythonFunctionOverload
        public FunctionDefinition FunctionDefinition { get; }
        public string Name { get; }
        public string Documentation => _documentationProvider?.Invoke(Name) ?? string.Empty;
        public string ReturnDocumentation { get; }
        public IReadOnlyList<IParameterInfo> Parameters { get; }
        public LocationInfo Location => _locationProvider?.Invoke(Name) ?? LocationInfo.Empty;
        public PythonMemberType MemberType => PythonMemberType.Function;

        public IMember GetReturnValue(IPythonInstance instance, IReadOnlyList<IMember> args) {
            if (!_fromAnnotation) {
                // First try supplied specialization callback.
                var rt = _returnValueProvider?.Invoke(args);
                if (!rt.IsUnknown()) {
                    return rt;
                }
            }

            // Then see if return value matches type of one of the input arguments.
            var t = _returnValue.GetPythonType();
            if (!(t is IPythonCallableArgumentType) && !t.IsUnknown()) {
                return _returnValue;
            }

            if (t is IPythonCallableArgumentType cat && args != null) {
                var rt = cat.ParameterIndex < args.Count ? args[cat.ParameterIndex] : null;
                if (!rt.IsUnknown()) {
                    return rt;
                }
            }
            return _returnValue;
        }
        #endregion
    }
}
