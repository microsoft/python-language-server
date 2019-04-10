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
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Types {
    class PythonPropertyType : PythonType, IPythonPropertyType {
        private IPythonFunctionOverload _getter;

        public PythonPropertyType(FunctionDefinition fd, Location location, IPythonType declaringType, bool isAbstract)
            : this(fd.Name, location, declaringType, isAbstract) {
            FunctionDefinition = fd;
        }

        public PythonPropertyType(string name, Location location, IPythonType declaringType, bool isAbstract)
            : base(name, location, string.Empty, BuiltinTypeId.Property) {
            DeclaringType = declaringType;
            IsAbstract = isAbstract;
        }

        #region IPythonType
        public override IPythonType DeclaringType { get; }
        public override PythonMemberType MemberType => PythonMemberType.Property;
        #endregion

        #region IPythonPropertyType
        public FunctionDefinition FunctionDefinition { get; }
        public override bool IsAbstract { get; }
        public bool IsReadOnly => true;
        public string Description 
            => Type == null ? Resources.PropertyOfUnknownType : Resources.PropertyOfType.FormatUI(Type.Name);
        public override IMember Call(IPythonInstance instance, string memberName, IArgumentSet args)
            => _getter.Call(args, instance?.GetPythonType() ?? DeclaringType);
        #endregion

        internal void AddOverload(IPythonFunctionOverload overload) => _getter = _getter ?? overload;
        public IPythonType Type => _getter?.Call(ArgumentSet.Empty, DeclaringType)?.GetPythonType();
    }
}
