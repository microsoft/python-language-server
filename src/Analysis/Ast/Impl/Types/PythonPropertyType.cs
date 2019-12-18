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

using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Types {
    internal sealed class PythonPropertyType : PythonType, IPythonPropertyType {
        public PythonPropertyType(FunctionDefinition fd, Location location, IPythonType declaringType, bool isAbstract)
            : this(fd.Name, location, fd.GetDocumentation(), declaringType, isAbstract) {
            declaringType.DeclaringModule.AddAstNode(this, fd);
        }

        public PythonPropertyType(string name, Location location, string documentation, IPythonType declaringType, bool isAbstract)
            : base(name, location, documentation, BuiltinTypeId.Property) {
            DeclaringType = declaringType;
            IsAbstract = isAbstract;
        }

        #region IPythonType
        public override PythonMemberType MemberType => PythonMemberType.Property;
        public override string QualifiedName => this.GetQualifiedName();
        #endregion

        #region IPythonPropertyType
        public FunctionDefinition FunctionDefinition => DeclaringModule.GetAstNode<FunctionDefinition>(this);
        public override bool IsAbstract { get; }
        public bool IsReadOnly => true;
        public IPythonType DeclaringType { get; }

        public override IMember Call(IPythonInstance instance, string memberName, IArgumentSet args)
                => Getter.Call(args, instance?.GetPythonType() ?? DeclaringType);

        public IMember ReturnType => Getter?.Call(ArgumentSet.WithoutContext, DeclaringType);
        #endregion

        internal void AddOverload(PythonFunctionOverload overload) => Getter = overload;
        internal PythonFunctionOverload Getter { get; private set; }
    }
}
