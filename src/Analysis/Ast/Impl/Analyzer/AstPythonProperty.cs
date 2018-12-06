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
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    class AstPythonProperty : AstPythonType, IPythonProperty {
        private IPythonFunctionOverload _getter;

        public AstPythonProperty(FunctionDefinition fd, IPythonModule declaringModule, IPythonType declaringType, LocationInfo location)
            : base(fd.Name, declaringModule, null, location) {
            FunctionDefinition = fd;
            DeclaringType = declaringType;
        }

        #region IMember
        public override PythonMemberType MemberType => PythonMemberType.Property;
        #endregion

        #region IPythonProperty
        public bool IsStatic => false;
        public IPythonType DeclaringType { get; }
        public string Description 
            => Type == null ? Resources.PropertyOfUnknownType : Resources.PropertyOfType.FormatUI(Type.Name);
        public FunctionDefinition FunctionDefinition { get; }
        #endregion

        internal void AddOverload(IPythonFunctionOverload overload) => _getter = _getter ?? overload;

        public void MakeSettable() => IsReadOnly = false;

        public IPythonType Type => _getter?.ReturnType.FirstOrDefault();

        public bool IsReadOnly { get; private set; } = true;
    }
}
