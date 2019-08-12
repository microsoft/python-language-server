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

using System.Linq;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Types {
    class PythonPropertyType : PythonType, IPythonPropertyType {
        private IPythonFunctionOverload _getter;
        private bool _isAbstract;

        public PythonPropertyType(FunctionDefinition fd, Location location, IPythonType declaringType)
            : this(fd.Name, location, declaringType) {
            declaringType.DeclaringModule.AddAstNode(this, fd);
            ProcessDecorators();
        }

        public PythonPropertyType(string name, Location location, IPythonType declaringType)
            : base(name, location, string.Empty, BuiltinTypeId.Property) {
            DeclaringType = declaringType;
        }

        private void ProcessDecorators() {
            foreach (var dec in (FunctionDefinition?.Decorators?.Decorators).MaybeEnumerate().OfType<NameExpression>()) {
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
                    case @"abstractproperty":
                        _isAbstract = true;
                        break;
                }
            }
        }

        #region IPythonType
        public override PythonMemberType MemberType => PythonMemberType.Property;
        #endregion

        #region IPythonPropertyType
        public FunctionDefinition FunctionDefinition => DeclaringModule.GetAstNode<FunctionDefinition>(this);
        public override bool IsAbstract => _isAbstract;
        public bool IsReadOnly => true;
        public IPythonType DeclaringType { get; }
        public bool IsStatic { get; private set; }
        public bool IsClassMethod { get; private set; }
        public string Description
            => Type == null ? Resources.PropertyOfUnknownType : Resources.PropertyOfType.FormatUI(Type.Name);
        public override IMember Call(IPythonInstance instance, string memberName, IArgumentSet args)
            => _getter?.Call(args, instance?.GetPythonType() ?? DeclaringType);
        #endregion

        internal void AddOverload(IPythonFunctionOverload overload) => _getter = _getter ?? overload;
        private IPythonType Type => _getter?.Call(ArgumentSet.WithoutContext, DeclaringType)?.GetPythonType();

    }
}
