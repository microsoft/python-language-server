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
using Microsoft.Python.Analysis.Analyzer.Evaluation;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Types {
    /// <summary>
    /// Delegate that is invoked to determine function return type dynamically.
    /// Can be used in specializations or when return type depends on actual arguments.
    /// </summary>
    /// <param name="declaringModule">Module making the call.</param>
    /// <param name="overload">Function overload the return value is requested for.</param>
    /// <param name="args">Call arguments.</param>
    /// <returns></returns>
    public delegate IMember ReturnValueProvider(
        IPythonModule declaringModule,
        IPythonFunctionOverload overload,
        IArgumentSet args);

    internal sealed class PythonFunctionOverload : LocatedMember, IPythonFunctionOverload {
        private readonly string _returnDocumentation;

        // Allow dynamic function specialization, such as defining return types for builtin
        // functions that are impossible to scrape and that are missing from stubs.
        //  Parameters: declaring module, overload for the return value, list of arguments.
        private ReturnValueProvider _returnValueProvider;

        // Return value can be an instance or a type info. Consider type(C()) returning
        // type info of C vs. return C() that returns an instance of C.
        private Func<string, string> _documentationProvider;
        private bool _fromAnnotation;

        public PythonFunctionOverload(FunctionDefinition fd, IPythonClassMember classMember, Location location)
            : this(fd.Name, location) {
            FunctionDefinition = fd;
            ClassMember = classMember;
            var ast = (location.Module as IDocument)?.Analysis.Ast;
            _returnDocumentation = ast != null ? fd.ReturnAnnotation?.ToCodeString(ast) : null;
        }

        public PythonFunctionOverload(string name, Location location) : base(location) {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        #region ILocatedMember
        public override PythonMemberType MemberType => PythonMemberType.Function;
        #endregion

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

        #region IPythonFunctionOverload
        public FunctionDefinition FunctionDefinition { get; }
        public IPythonClassMember ClassMember { get; }
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

        public string GetReturnDocumentation(IPythonType self = null) {
            if (self == null) {
                return _returnDocumentation;
            }
            var returnType = StaticReturnValue.GetPythonType();
            switch (returnType) {
                case PythonClassType cls when cls.IsGeneric(): {
                        // -> A[_T1, _T2, ...]
                        // Match arguments 
                        var typeArgs = cls.GenericParameters.Keys
                            .Select(n => cls.GenericParameters.TryGetValue(n, out var t) ? t : null)
                            .ExcludeDefault()
                            .ToArray();
                        var specificReturnValue = cls.CreateSpecificType(new ArgumentSet(typeArgs));
                        return specificReturnValue.Name;
                    }
                case IGenericTypeDefinition gtp1 when self is IPythonClassType cls: {
                        // -> _T
                        if (cls.GenericParameters.TryGetValue(gtp1.Name, out var specificType)) {
                            return specificType.Name;
                        }
                        // Try returning the constraint
                        // TODO: improve this, the heuristic is pretty basic and tailored to simple func(_T) -> _T
                        var name = StaticReturnValue.GetPythonType()?.Name;
                        var typeDefVar = DeclaringModule.Analysis.GlobalScope.Variables[name];
                        if (typeDefVar?.Value is IGenericTypeDefinition gtp2) {
                            var t = gtp2.Constraints.FirstOrDefault();
                            if (t != null) {
                                return t.Name;
                            }
                        }
                        break;
                    }
            }
            return _returnDocumentation;
        }

        public IReadOnlyList<IParameterInfo> Parameters { get; private set; } = Array.Empty<IParameterInfo>();
        public IMember StaticReturnValue { get; private set; }

        public IMember Call(IArgumentSet args, IPythonType self, Node callLocation = null) {
            if (!_fromAnnotation) {
                // First try supplied specialization callback.
                var rt = _returnValueProvider?.Invoke(DeclaringModule, this, args);
                if (!rt.IsUnknown()) {
                    return rt;
                }
            }

            // If function returns generic, determine actual type based on the passed in specific type (self).
            // If there is no self and no declaring type, the function is standalone.
            if (self == null && StaticReturnValue.IsGeneric() && Parameters.Any(p => p.IsGeneric)) {
                return null; // Evaluate standalone generic with arguments instead.
            }
            if (!(self is IPythonClassType selfClassType)) {
                return StaticReturnValue;
            }

            var returnType = StaticReturnValue.GetPythonType();
            switch (returnType) {
                case PythonClassType cls when cls.IsGeneric():
                    // -> A[_T1, _T2, ...]
                    // Match arguments
                    IReadOnlyList<IPythonType> typeArgs = null;
                    var classGenericParameters = selfClassType.GenericParameters.Keys.ToArray();
                    if (classGenericParameters.Length > 0) {
                        // Declaring class is specific and provides definitions of generic parameters
                        typeArgs = classGenericParameters
                            .Select(n => selfClassType.GenericParameters.TryGetValue(n, out var t) ? t : null)
                            .ExcludeDefault()
                            .ToArray();
                    } else {
                        typeArgs = ExpressionEval.GetTypeArgumentsFromParameters(this, args);
                    }

                    if (typeArgs != null) {
                        var specificReturnValue = cls.CreateSpecificType(new ArgumentSet(typeArgs));
                        return new PythonInstance(specificReturnValue);
                    }
                    break;

                case IGenericTypeDefinition gtp1: {
                        // -> _T
                        if (selfClassType.GenericParameters.TryGetValue(gtp1.Name, out var specificType)) {
                            return new PythonInstance(specificType);
                        }
                        // Try returning the constraint
                        // TODO: improve this, the heuristic is pretty basic and tailored to simple func(_T) -> _T
                        var name = StaticReturnValue.GetPythonType()?.Name;
                        var typeDefVar = DeclaringModule.Analysis.GlobalScope.Variables[name];
                        if (typeDefVar?.Value is IGenericTypeDefinition gtp2) {
                            // See if the instance (self) type satisfies one of the constraints.
                            return selfClassType.Mro.Any(b => gtp2.Constraints.Any(c => c.Equals(b)))
                                ? selfClassType
                                : gtp2.Constraints.FirstOrDefault();
                        }

                        break;
                    }
            }
            return StaticReturnValue;
        }
        #endregion
    }
}
