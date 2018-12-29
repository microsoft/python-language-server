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
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Types {
    /// <summary>
    /// Delegates most of the methods to the wrapped/inner class.
    /// </summary>
    internal class PythonTypeWrapper : IPythonType, ILocatedMember, IHasQualifiedName {
        protected IPythonType InnerType { get; }

        public PythonTypeWrapper(IPythonType type)
            : this(type, type.DeclaringModule) {
        }

        public PythonTypeWrapper(IPythonType type, IPythonModule declaringModule) {
            InnerType = type ?? throw new ArgumentNullException(nameof(type));
            DeclaringModule = declaringModule;
        }

        #region IPythonType
        public virtual string Name => InnerType.Name;
        public IPythonModule DeclaringModule { get; }
        public virtual string Documentation => InnerType.Documentation;
        public virtual  BuiltinTypeId TypeId => InnerType.TypeId;
        public virtual  PythonMemberType MemberType => InnerType.MemberType;
        public virtual  bool IsBuiltin => InnerType.IsBuiltin;
        public virtual IMember CreateInstance(IPythonInterpreter interpreter, LocationInfo location, params object[] args)
            => new PythonInstance(this, location);
        #endregion

        #region ILocatedMember
        public virtual LocationInfo Location => (InnerType as ILocatedMember)?.Location ?? LocationInfo.Empty;
        #endregion

        #region IMemberContainer
        public virtual IMember GetMember(string name) => InnerType.GetMember(name);
        public virtual IEnumerable<string> GetMemberNames() => InnerType.GetMemberNames();
        #endregion

        #region IHasQualifiedName
        public virtual string FullyQualifiedName => (InnerType as IHasQualifiedName)?.FullyQualifiedName;
        public virtual KeyValuePair<string, string> FullyQualifiedNamePair => (InnerType as IHasQualifiedName)?.FullyQualifiedNamePair ?? default;
        #endregion
    }
}
