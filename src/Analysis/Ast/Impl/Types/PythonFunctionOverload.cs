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
using System.Diagnostics;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Types {
    /// <summary>
    /// Delegate that is invoked to determine function return type dynamically.
    /// Can be used in specializations or when return type depends on actual arguments.
    /// </summary>
    /// <param name="declaringModule">Module making the call.</param>
    /// <param name="overload">Function overload the return value is requested for.</param>
    /// <param name="location">Call location, if any.</param>
    /// <param name="args">Call arguments.</param>
    /// <returns></returns>
    public delegate IMember ReturnValueProvider(
        IPythonModule declaringModule,
        IPythonFunctionOverload overload,
        LocationInfo location,
        IReadOnlyList<IMember> args);

    internal sealed class PythonFunctionOverload : IPythonFunctionOverload, ILocatedMember {
        private readonly Func<string, LocationInfo> _locationProvider;
        private readonly IPythonModule _declaringModule;

        // Allow dynamic function specialization, such as defining return types for builtin
        // functions that are impossible to scrape and that are missing from stubs.
        //  Parameters: declaring module, overload for the return value, list of arguments.
        private ReturnValueProvider _returnValueProvider;

        // Return value can be an instance or a type info. Consider type(C()) returning
        // type info of C vs. return C() that returns an instance of C.
        private Func<string, string> _documentationProvider;
        private bool _fromAnnotation;

        public PythonFunctionOverload(string name, IPythonModule declaringModule, LocationInfo location, string returnDocumentation = null)
            : this(name, declaringModule, _ => location ?? LocationInfo.Empty, returnDocumentation) {
            _declaringModule = declaringModule;
        }

        public PythonFunctionOverload(FunctionDefinition fd, IPythonModule declaringModule, LocationInfo location, string returnDocumentation = null)
            : this(fd.Name, declaringModule, _ => location, returnDocumentation) {
            FunctionDefinition = fd;
        }

        public PythonFunctionOverload(string name, IPythonModule declaringModule,
            Func<string, LocationInfo> locationProvider, string returnDocumentation = null) {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            ReturnDocumentation = returnDocumentation;
            _declaringModule = declaringModule;
            _locationProvider = locationProvider;
        }

        internal void SetParameters(IReadOnlyList<IParameterInfo> parameters) => Parameters = parameters;

        internal void SetDocumentationProvider(Func<string, string> documentationProvider) 
            => _documentationProvider = _documentationProvider ?? documentationProvider;

        internal void AddReturnValue(IMember value) {
            if (value.IsUnknown()) {
                return; // Don't add useless values.
            }
            if (StaticReturnValue.IsUnknown()) {
                SetReturnValue(value, false);
                return;
            }
            // If return value is set from annotation, it should not be changing.
            var currentType = StaticReturnValue.GetPythonType();
            var valueType = value.GetPythonType();
            if (!_fromAnnotation && !currentType.Equals(valueType)) {
                var type = PythonUnionType.Combine(currentType, valueType);
                // Track instance vs type info.
                StaticReturnValue = value is IPythonInstance ? new PythonInstance(type) : (IMember)type;
            }
        }

        internal void SetReturnValue(IMember value, bool fromAnnotation) {
            StaticReturnValue = value;
            _fromAnnotation = fromAnnotation;
        }

        internal void SetReturnValueProvider(ReturnValueProvider provider)
            => _returnValueProvider = provider;

        internal IMember StaticReturnValue { get; private set; }


        #region IPythonFunctionOverload
        public FunctionDefinition FunctionDefinition { get; }
        public string Name { get; }

        public string Documentation {
            get {
                var s = _documentationProvider?.Invoke(Name);
                if (string.IsNullOrEmpty(s)) {
                    s = FunctionDefinition.GetDocumentation();
                }
                return s ?? string.Empty;
            }
        }

        public string ReturnDocumentation { get; }
        public IReadOnlyList<IParameterInfo> Parameters { get; private set; } = Array.Empty<IParameterInfo>();
        public LocationInfo Location => _locationProvider?.Invoke(Name) ?? LocationInfo.Empty;
        public PythonMemberType MemberType => PythonMemberType.Function;

        public IMember GetReturnValue(LocationInfo callLocation, IReadOnlyList<IMember> args) {
            if (!_fromAnnotation) {
                // First try supplied specialization callback.
                var rt = _returnValueProvider?.Invoke(_declaringModule, this, callLocation, args);
                if (!rt.IsUnknown()) {
                    return rt;
                }
            }

            // Then see if return value matches type of one of the input arguments.
            var t = StaticReturnValue.GetPythonType();
            if (!(t is IFunctionArgumentType) && !t.IsUnknown()) {
                return StaticReturnValue;
            }

            if (t is IFunctionArgumentType cat && args != null) {
                var rt = cat.ParameterIndex < args.Count ? args[cat.ParameterIndex] : null;
                if (!rt.IsUnknown()) {
                    return rt;
                }
            }
            return StaticReturnValue;
        }
        #endregion

        private sealed class ReturnValueCache {
            private const int MaxResults = 10;
            private readonly Dictionary<ArgumentSet, IMember> _results = new Dictionary<ArgumentSet, IMember>(new ArgumentSetComparer());

            public bool TryGetResult(IReadOnlyList<IMember> args, out IMember result) 
                => _results.TryGetValue(new ArgumentSet(args), out result);

            public void AddResult(IReadOnlyList<IMember> args, out IMember result) {
                var key = new ArgumentSet(args);
                Debug.Assert(!_results.ContainsKey(key));
                _results[key] = result;
            }
        }
    }
}
