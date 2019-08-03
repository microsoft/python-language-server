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
        private bool _fromAnnotation;

        public PythonFunctionOverload(IPythonClassMember cm, FunctionDefinition fd, Location location, string returnDocumentation)
            : this(cm.Name, location) {
            ClassMember = cm;
            Documentation = fd.GetDocumentation();
            cm.DeclaringModule.AddAstNode(this, fd);
            _returnDocumentation = returnDocumentation;
        }

        public PythonFunctionOverload(string name, Location location) : base(location) {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        #region ILocatedMember
        public override PythonMemberType MemberType => PythonMemberType.Function;
        #endregion

        internal void SetParameters(IReadOnlyList<IParameterInfo> parameters) => Parameters = parameters;

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

        internal void SetReturnValueProvider(ReturnValueProvider provider) => _returnValueProvider = provider;
        internal void SetDocumentation(string documentation) => Documentation = documentation;

        #region IPythonFunctionOverload
        public FunctionDefinition FunctionDefinition => ClassMember?.DeclaringModule?.GetAstNode<FunctionDefinition>(this);
        public IPythonClassMember ClassMember { get; }
        public string Name { get; }
        public string Documentation { get; private set; }

        public string GetReturnDocumentation(IPythonType self = null) {
            if (self != null) {
                var returnType = GetSpecificReturnType(self as IPythonClassType, null);
                if (!returnType.IsUnknown()) {
                    return returnType.GetPythonType().Name;
                }
            }
            return _returnDocumentation;
        }

        public IReadOnlyList<IParameterInfo> Parameters { get; private set; } = Array.Empty<IParameterInfo>();
        public IMember StaticReturnValue { get; private set; }

        public IMember Call(IArgumentSet args, IPythonType self) {
            if (!_fromAnnotation) {
                // First try supplied specialization callback.
                var rt = _returnValueProvider?.Invoke(DeclaringModule, this, args);
                if (!rt.IsUnknown()) {
                    return rt;
                }
            }

            return GetSpecificReturnType(self as IPythonClassType, args);
        }
        #endregion

        private IMember GetSpecificReturnType(IPythonClassType selfClassType, IArgumentSet args) {
            var returnValueType = StaticReturnValue.GetPythonType();
            switch (returnValueType) {
                case PythonClassType cls when cls.IsGeneric:
                    return CreateSpecificReturnFromClassType(selfClassType, cls, args); // -> A[_T1, _T2, ...]

                case IGenericType gt when gt.IsGeneric && args != null: // -> CLASS[T] on standalone function (i.e. -> List[T]).
                    var typeArgs = ExpressionEval.GetTypeArgumentsFromParameters(this, args);
                    if (typeArgs != null) {
                        return gt.CreateSpecificType(new ArgumentSet(typeArgs, args.Expression, args.Eval));
                    }
                    break;

                case IGenericTypeParameter gtd1 when selfClassType != null:
                    return CreateSpecificReturnFromTypeVar(selfClassType, gtd1); // -> _T

                case IGenericTypeParameter gtd2 when args != null: // -> T on standalone function.
                    return args.Arguments.FirstOrDefault(a => gtd2.Equals(a.Type))?.Value as IMember;
            }

            return StaticReturnValue;
        }

        private IMember CreateSpecificReturnFromClassType(IPythonClassType selfClassType, PythonClassType returnClassType, IArgumentSet args) {
            // -> A[_T1, _T2, ...]
            // Match arguments
            IReadOnlyList<IPythonType> typeArgs = null;
            var classGenericParameters = selfClassType?.GenericParameters.Keys.ToArray() ?? Array.Empty<IGenericTypeParameter>();
            if (classGenericParameters.Length > 0 && selfClassType != null) {
                // Declaring class is specific and provides definitions of generic parameters
                typeArgs = classGenericParameters
                    .Select(n => selfClassType.GenericParameters.TryGetValue(n, out var t) ? t : null)
                    .ExcludeDefault()
                    .ToArray();
            } else if (args != null) {
                typeArgs = ExpressionEval.GetTypeArgumentsFromParameters(this, args);
            }

            if (typeArgs != null) {
                var specificReturnValue = returnClassType.CreateSpecificType(new ArgumentSet(typeArgs, args?.Expression, args?.Eval));
                return new PythonInstance(specificReturnValue);
            }

            return null;
        }

        private IMember CreateSpecificReturnFromTypeVar(IPythonClassType selfClassType, IGenericTypeParameter returnType) {
            if (selfClassType.GenericParameters.TryGetValue(returnType, out var specificType)) {
                return new PythonInstance(specificType);
            }

            // Find base class type in which function was declared
            var baseType = selfClassType.Mro
                .OfType<IPythonClassType>()
                .Skip(1)
                .Where(b => b.GetMember(ClassMember.Name) != null && b.GenericParameters.ContainsKey(returnType))
                .FirstOrDefault();

            // Try and infer return value from base class
            if (baseType != null && baseType.GenericParameters.TryGetValue(returnType, out specificType)) {
                return new PythonInstance(specificType);
            }

            // look at function declaring type and select from selfClassType.bases where first matches 

            // Try returning the constraint
            // TODO: improve this, the heuristic is pretty basic and tailored to simple func(_T) -> _T
            var name = StaticReturnValue.GetPythonType()?.Name;
            var typeDefVar = DeclaringModule.Analysis.GlobalScope.Variables[name];
            if (typeDefVar?.Value is IGenericTypeParameter gtp2) {
                // See if the instance (self) type satisfies one of the constraints.
                return selfClassType.Mro.Any(b => gtp2.Constraints.Any(c => c.Equals(b)))
                    ? selfClassType
                    : gtp2.Constraints.FirstOrDefault();
            }

            return null;
        }
    }
}
