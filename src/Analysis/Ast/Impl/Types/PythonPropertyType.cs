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

using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Types {
    class PythonPropertyType : PythonType, IPythonPropertyType {
        private IPythonFunctionOverload _getter;

        public PythonPropertyType(FunctionDefinition fd, IPythonModule declaringModule, IPythonType declaringType, bool isAbstract, LocationInfo location)
            : base(fd.Name, declaringModule, null, location) {
            FunctionDefinition = fd;
            DeclaringType = declaringType;
            IsAbstract = isAbstract;
        }

        #region IPythonType
        public override PythonMemberType MemberType => PythonMemberType.Property;
        #endregion

        #region IPythonPropertyType
        public bool IsAbstract { get; }
        public bool IsReadOnly { get; private set; } = true;
        public IPythonType DeclaringType { get; }
        public string Description 
            => Type == null ? Resources.PropertyOfUnknownType : Resources.PropertyOfType.FormatUI(Type.Name);
        public FunctionDefinition FunctionDefinition { get; }
        #endregion

        internal void AddOverload(IPythonFunctionOverload overload) => _getter = _getter ?? overload;

        public void MakeSettable() => IsReadOnly = false;

        public IPythonType Type => _getter?.GetReturnValue(null)?.GetPythonType();
    }
}
