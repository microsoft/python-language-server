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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonFunction : AstPythonType, IPythonFunction {
        private readonly List<IPythonFunctionOverload> _overloads = new List<IPythonFunctionOverload>();
        private readonly string _doc;
        private readonly object _lock = new object();

        public AstPythonFunction(
            FunctionDefinition fd,
            IPythonModule declaringModule,
            IPythonType declaringType,
            ILocationInfo loc
        ) : base(fd.Name, declaringModule, fd.Documentation, loc,
                declaringType != null ? BuiltinTypeId.Method : BuiltinTypeId.Function, true) {

            FunctionDefinition = fd;
            DeclaringType = declaringType;

            if (Name == "__init__") {
                _doc = declaringType?.Documentation;
            }

            foreach (var dec in (FunctionDefinition.Decorators?.Decorators).MaybeEnumerate().ExcludeDefault().OfType<NameExpression>()) {
                if (dec.Name == "classmethod") {
                    IsClassMethod = true;
                } else if (dec.Name == "staticmethod") {
                    IsStatic = true;
                }
            }
        }

        #region IMember
        public override PythonMemberType MemberType
            => TypeId == BuiltinTypeId.Function ? PythonMemberType.Function : PythonMemberType.Method;
        #endregion

        #region IPythonFunction
        public FunctionDefinition FunctionDefinition { get; }
        public IPythonType DeclaringType { get; }
        public override string Documentation => _doc ?? _overloads.FirstOrDefault()?.Documentation;
        public virtual bool IsClassMethod { get; }
        public virtual bool IsStatic { get; }
        public IReadOnlyList<IPythonFunctionOverload> Overloads => _overloads.ToArray();
        #endregion

        #region IHasQualifiedName
        public override string FullyQualifiedName => FullyQualifiedNamePair.CombineNames();
        public override KeyValuePair<string, string> FullyQualifiedNamePair =>
            new KeyValuePair<string, string>((DeclaringType as IHasQualifiedName)?.FullyQualifiedName ?? DeclaringType?.Name ?? DeclaringModule?.Name, Name);
        #endregion

        internal virtual void AddOverload(IPythonFunctionOverload overload) {
            lock (_lock) {
                _overloads.Add(overload);
            }
        }

        internal IPythonFunction ToUnbound() => new AstPythonUnboundMethod(this);

        /// <summary>
        /// Represents unbound method, such in C.f where C is class rather than the instance.
        /// </summary>
        class AstPythonUnboundMethod : AstPythonTypeWrapper, IPythonFunction {
            private readonly IPythonFunction _pf;

            public AstPythonUnboundMethod(IPythonFunction function) : base(function, function.DeclaringModule) {
                _pf = function;
            }

            public FunctionDefinition FunctionDefinition => _pf.FunctionDefinition;
            public IPythonType DeclaringType => _pf.DeclaringType;
            public bool IsStatic => _pf.IsStatic;
            public bool IsClassMethod => _pf.IsClassMethod;
            public IReadOnlyList<IPythonFunctionOverload> Overloads => _pf.Overloads;
            public override BuiltinTypeId TypeId => BuiltinTypeId.Function;
            public override PythonMemberType MemberType => PythonMemberType.Function;
        }
    }
}
