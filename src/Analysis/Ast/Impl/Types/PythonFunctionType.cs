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
using System.Diagnostics;
using System.Linq;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Types {
    [DebuggerDisplay("Function {Name} ({TypeId})")]
    internal sealed class PythonFunctionType : PythonType, IPythonFunctionType {
        private ImmutableArray<IPythonFunctionOverload> _overloads = ImmutableArray<IPythonFunctionOverload>.Empty;
        private bool _isAbstract;
        private bool _isSpecialized;

        /// <summary>
        /// Creates function for specializations
        /// </summary>
        public static PythonFunctionType Specialize(string name, IPythonModule declaringModule, string documentation)
            => new PythonFunctionType(name, new Location(declaringModule, default), documentation, true);

        private PythonFunctionType(string name, Location location, string documentation, bool isSpecialized = false) :
            base(name, location, documentation ?? string.Empty, BuiltinTypeId.Function) {
            Check.ArgumentNotNull(nameof(location), location.Module);
            _isSpecialized = isSpecialized;
        }

        /// <summary>
        /// Creates function type to use in special cases when function is dynamically
        /// created, such as in specializations and custom iterators, without the actual
        /// function definition in the AST.
        /// </summary>
        public PythonFunctionType(
            string name,
            Location location,
            IPythonType declaringType,
            string documentation
        ) : base(name, location, documentation, declaringType != null ? BuiltinTypeId.Method : BuiltinTypeId.Function) {
            DeclaringType = declaringType;
        }

        public PythonFunctionType(
            FunctionDefinition fd,
            IPythonType declaringType,
            Location location
        ) : base(fd.Name, location,
            fd.Name == "__init__" ? (declaringType?.Documentation ?? fd.GetDocumentation()) : fd.GetDocumentation(),
            declaringType != null ? BuiltinTypeId.Method : BuiltinTypeId.Function) {
            DeclaringType = declaringType;

            location.Module.AddAstNode(this, fd);
            ProcessDecorators(fd);
        }

        #region IPythonType
        public override PythonMemberType MemberType
            => TypeId == BuiltinTypeId.Function ? PythonMemberType.Function : PythonMemberType.Method;

        public override IMember Call(IPythonInstance instance, string memberName, IArgumentSet args) {
            // Now we can go and find overload with matching arguments.
            var overload = Overloads[args.OverloadIndex];
            return overload?.Call(args, instance?.GetPythonType() ?? DeclaringType);
        }

        internal override void SetDocumentation(string documentation) {
            foreach (var o in Overloads) {
                (o as PythonFunctionOverload)?.SetDocumentation(documentation);
            }
            base.SetDocumentation(documentation);
        }

        #endregion

        #region IPythonFunction
        public FunctionDefinition FunctionDefinition => DeclaringModule.GetAstNode<FunctionDefinition>(this);
        public IPythonType DeclaringType { get; }
        public override string Documentation => (_overloads.Count > 0 ? _overloads[0].Documentation : default) ?? base.Documentation;
        public bool IsClassMethod { get; private set; }
        public bool IsStatic { get; private set; }
        public override bool IsAbstract => _isAbstract;
        public override bool IsSpecialized => _isSpecialized;

        public bool IsOverload { get; private set; }
        public bool IsStub { get; internal set; }
        public bool IsUnbound => DeclaringType == null;

        public IReadOnlyList<IPythonFunctionOverload> Overloads => _overloads;
        #endregion

        internal void Specialize(string[] dependencies) {
            _isSpecialized = true;
            Dependencies = dependencies != null
                ? ImmutableArray<string>.Create(dependencies)
                : ImmutableArray<string>.Empty;
        }

        internal ImmutableArray<string> Dependencies { get; private set; } = ImmutableArray<string>.Empty;

        internal void AddOverload(IPythonFunctionOverload overload)
            => _overloads = _overloads.Count > 0 ? _overloads.Add(overload) : ImmutableArray<IPythonFunctionOverload>.Create(overload);

        internal IPythonFunctionType ToUnbound() => new PythonUnboundMethod(this);

        private void ProcessDecorators(FunctionDefinition fd) {
            // TODO: warn about incompatible combinations.
            foreach (var dec in (fd.Decorators?.Decorators).MaybeEnumerate()) {
                switch (dec) {
                    case NameExpression n:
                        ProcessDecorator(n.Name);
                        break;
                    case MemberExpression m:
                        ProcessDecorator(m.Name);
                        break;
                }
            }
        }

        private void ProcessDecorator(string decorator) {
            switch (decorator) {
                case @"staticmethod":
                    IsStatic = true;
                    break;
                case @"classmethod":
                    IsClassMethod = true;
                    break;
                case @"abstractmethod":
                    _isAbstract = true;
                    break;
                case @"abstractstaticmethod":
                    IsStatic = true;
                    _isAbstract = true;
                    break;
                case @"abstractclassmethod":
                    IsClassMethod = true;
                    _isAbstract = true;
                    break;
                case @"overload":
                    IsOverload = true;
                    break;
                case @"property":
                case @"abstractproperty":
                    Debug.Assert(false, "Found property attribute while processing function. Properties should be handled in the respective class.");
                    break;
            }
        }

        /// <summary>
        /// Represents unbound method, such in C.f where C is class rather than the instance.
        /// </summary>
        private sealed class PythonUnboundMethod : PythonTypeWrapper, IPythonFunctionType {
            private readonly IPythonFunctionType _pf;

            public PythonUnboundMethod(IPythonFunctionType function) : base(function, function.DeclaringModule) {
                _pf = function;
            }

            public FunctionDefinition FunctionDefinition => _pf.FunctionDefinition;
            public IPythonType DeclaringType => _pf.DeclaringType;
            public bool IsStatic => _pf.IsStatic;
            public bool IsClassMethod => _pf.IsClassMethod;
            public bool IsOverload => _pf.IsOverload;
            public bool IsStub => _pf.IsStub;
            public bool IsUnbound => true;

            public IReadOnlyList<IPythonFunctionOverload> Overloads => _pf.Overloads;
            public override BuiltinTypeId TypeId => BuiltinTypeId.Function;
            public override PythonMemberType MemberType => PythonMemberType.Function;
        }
    }
}
