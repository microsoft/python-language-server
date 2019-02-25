﻿// Copyright(c) Microsoft Corporation
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
using System.Linq;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Types {
    [DebuggerDisplay("Function {Name} ({TypeId})")]
    internal class PythonFunctionType : PythonType, IPythonFunctionType {
        private readonly List<IPythonFunctionOverload> _overloads = new List<IPythonFunctionOverload>();
        private readonly object _lock = new object();
        private bool _isAbstract;
        private bool _isSpecialized;

        /// <summary>
        /// Creates function for specializations
        /// </summary>
        public static PythonFunctionType ForSpecialization(string name, IPythonModule declaringModule)
            => new PythonFunctionType(name, declaringModule, true);

        private PythonFunctionType(string name, IPythonModule declaringModule, bool isSpecialized = false) :
            base(name, declaringModule, null, LocationInfo.Empty, BuiltinTypeId.Function) {
            DeclaringType = declaringModule;
            _isSpecialized = isSpecialized;
        }

        /// <summary>
        /// Creates function type to use in special cases when function is dynamically
        /// created, such as in specializations and custom iterators, without the actual
        /// function definition in the AST.
        /// </summary>
        public PythonFunctionType(
            string name,
            IPythonModule declaringModule,
            IPythonType declaringType,
            string documentation,
            LocationInfo location = null
        ) : this(name, declaringModule, declaringType, _ => documentation, _ => location ?? LocationInfo.Empty) { }

        /// <summary>
        /// Creates function type to use in special cases when function is dynamically
        /// created, such as in specializations and custom iterators, without the actual
        /// function definition in the AST.
        /// </summary>
        public PythonFunctionType(
            string name,
            IPythonModule declaringModule,
            IPythonType declaringType,
            Func<string, string> documentationProvider,
            Func<string, LocationInfo> locationProvider,
            IPythonFunctionOverload overload = null
        ) : base(name, declaringModule, documentationProvider, locationProvider,
            declaringType != null ? BuiltinTypeId.Method : BuiltinTypeId.Function) {
            DeclaringType = declaringType;
            if (overload != null) {
                AddOverload(overload);
            }
        }

        public PythonFunctionType(
            FunctionDefinition fd,
            IPythonModule declaringModule,
            IPythonType declaringType,
            LocationInfo location = null
        ) : base(fd.Name, declaringModule, fd.Documentation, location ?? LocationInfo.Empty,
            declaringType != null ? BuiltinTypeId.Method : BuiltinTypeId.Function) {

            FunctionDefinition = fd;
            DeclaringType = declaringType;
            ProcessDecorators(fd);
        }

        #region IPythonType

        public override PythonMemberType MemberType
            => TypeId == BuiltinTypeId.Function ? PythonMemberType.Function : PythonMemberType.Method;

        public override IMember Call(IPythonInstance instance, string memberName, IArgumentSet args) {
            // Now we can go and find overload with matching arguments.
            var overload = Overloads[args.OverloadIndex];
            return overload?.Call(args, instance?.GetPythonType() ?? DeclaringType, instance?.Location);
        }

        internal override void SetDocumentationProvider(Func<string, string> provider) {
            foreach (var o in Overloads) {
                (o as PythonFunctionOverload)?.SetDocumentationProvider(provider);
            }

            base.SetDocumentationProvider(provider);
        }

        #endregion

        #region IPythonFunction
        public FunctionDefinition FunctionDefinition { get; }
        public IPythonType DeclaringType { get; }
        public override string Documentation {
            get {
                var doc = "__init__".EqualsOrdinal(FunctionDefinition?.Name) ? DeclaringType?.Documentation : null;
                return doc ?? _overloads.FirstOrDefault()?.Documentation;
            }
        }

        public virtual bool IsClassMethod { get; private set; }
        public virtual bool IsStatic { get; private set; }
        public override bool IsAbstract => _isAbstract;
        public override bool IsSpecialized => _isSpecialized;

        public bool IsOverload { get; private set; }
        public bool IsStub { get; internal set; }
        public bool IsUnbound => DeclaringType == null;

        public IReadOnlyList<IPythonFunctionOverload> Overloads => _overloads.ToArray();
        #endregion

        #region IHasQualifiedName
        public override string FullyQualifiedName => FullyQualifiedNamePair.CombineNames();
        public override KeyValuePair<string, string> FullyQualifiedNamePair =>
            new KeyValuePair<string, string>((DeclaringType as IHasQualifiedName)?.FullyQualifiedName ?? DeclaringType?.Name ?? DeclaringModule?.Name, Name);
        #endregion

        internal void Specialize(string[] dependencies) {
            _isSpecialized = true;
            Dependencies = dependencies != null
                ? ImmutableArray<string>.Create(dependencies)
                : ImmutableArray<string>.Empty;
        }

        internal ImmutableArray<string> Dependencies { get; private set; } = ImmutableArray<string>.Empty;

        internal void AddOverload(IPythonFunctionOverload overload) {
            lock (_lock) {
                _overloads.Add(overload);
            }
        }

        internal IPythonFunctionType ToUnbound() => new PythonUnboundMethod(this);

        private void ProcessDecorators(FunctionDefinition fd) {
            foreach (var dec in (fd.Decorators?.Decorators).MaybeEnumerate().OfType<NameExpression>()) {
                // TODO: warn about incompatible combinations.
                switch (dec.Name) {
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
