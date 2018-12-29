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
using System.Linq;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Types {
    [DebuggerDisplay("Function {Name} ({TypeId})")]
    internal class PythonFunctionType : PythonType, IPythonFunctionType {
        private readonly List<IPythonFunctionOverload> _overloads = new List<IPythonFunctionOverload>();
        private readonly string _doc;
        private readonly object _lock = new object();

        /// <summary>
        /// Creates function for specializations
        /// </summary>
        public static PythonFunctionType ForSpecialization(string name, IPythonModuleType declaringModule) 
            => new PythonFunctionType(name, declaringModule);

        private PythonFunctionType(string name, IPythonModuleType declaringModule): 
            base(name, declaringModule, null, LocationInfo.Empty, BuiltinTypeId.Function) {
            DeclaringType = declaringModule;
        }

        public PythonFunctionType(
            string name,
            IPythonModuleType declaringModule,
            IPythonType declaringType,
            string documentation,
            LocationInfo location = null
        ) : this(name, declaringModule, declaringType, _ => documentation, _ => location ?? LocationInfo.Empty) { }

        public PythonFunctionType(
            string name,
            IPythonModuleType declaringModule,
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
            IPythonModuleType declaringModule,
            IPythonType declaringType,
            LocationInfo location = null
        ) : base(fd.Name, declaringModule, fd.Documentation, location ?? LocationInfo.Empty,
                declaringType != null ? BuiltinTypeId.Method : BuiltinTypeId.Function) {

            FunctionDefinition = fd;
            DeclaringType = declaringType;

            if (fd.Name == "__init__") {
                _doc = declaringType?.Documentation;
            }

            ProcessDecorators(fd);
        }

        #region IPythonType
        public override PythonMemberType MemberType
            => TypeId == BuiltinTypeId.Function ? PythonMemberType.Function : PythonMemberType.Method;
        #endregion

        #region IPythonFunction
        public FunctionDefinition FunctionDefinition { get; }
        public IPythonType DeclaringType { get; }
        public override string Documentation => _doc ?? _overloads.FirstOrDefault()?.Documentation;
        public virtual bool IsClassMethod { get; private set; }
        public virtual bool IsStatic { get; private set; }
        public virtual bool IsAbstract { get; private set; }
        public IReadOnlyList<IPythonFunctionOverload> Overloads => _overloads.ToArray();
        #endregion

        #region IHasQualifiedName
        public override string FullyQualifiedName => FullyQualifiedNamePair.CombineNames();
        public override KeyValuePair<string, string> FullyQualifiedNamePair =>
            new KeyValuePair<string, string>((DeclaringType as IHasQualifiedName)?.FullyQualifiedName ?? DeclaringType?.Name ?? DeclaringModule?.Name, Name);
        #endregion

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
                        IsAbstract = true;
                        break;
                    case @"abstractstaticmethod":
                        IsStatic = true;
                        IsAbstract = true;
                        break;
                    case @"abstractclassmethod":
                        IsClassMethod = true;
                        IsAbstract = true;
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
            public bool IsAbstract => _pf.IsAbstract;

            public IReadOnlyList<IPythonFunctionOverload> Overloads => _pf.Overloads;
            public override BuiltinTypeId TypeId => BuiltinTypeId.Function;
            public override PythonMemberType MemberType => PythonMemberType.Function;
        }
    }
}
