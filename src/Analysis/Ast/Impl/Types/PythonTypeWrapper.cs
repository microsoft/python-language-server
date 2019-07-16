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
using Microsoft.Python.Core.Text;

namespace Microsoft.Python.Analysis.Types {
    /// <summary>
    /// Delegates most of the methods to the wrapped/inner class.
    /// </summary>
    internal class PythonTypeWrapper : IPythonType {
        private readonly BuiltinTypeId _builtinTypeId;
        private IPythonType _innerType;

        protected IPythonType InnerType 
            => _innerType ?? (_innerType = DeclaringModule.Interpreter.GetBuiltinType(_builtinTypeId));

        /// <summary>
        /// Creates delegate type wrapper over an existing type.
        /// Use dedicated constructor for wrapping builtin types.
        /// </summary>
        public PythonTypeWrapper(IPythonType type) : this(type, type.DeclaringModule) {
        }

        /// <summary>
        /// Creates delegate type wrapper over an existing type.
        /// Use dedicated constructor for wrapping builtin types.
        /// </summary>
        public PythonTypeWrapper(IPythonType type, IPythonModule declaringModule) {
            _innerType = type ?? throw new ArgumentNullException(nameof(type));
            DeclaringModule = declaringModule;
        }

        /// <summary>
        /// Creates type wrapper for a built-in type. This is preferable way to
        /// wrap builtins since it can be done when builtins module is not loaded
        /// yet - such as when builtins module itself is being imported or specialized.
        /// </summary>
        public PythonTypeWrapper(BuiltinTypeId builtinTypeId, IPythonModule declaringModule) {
            DeclaringModule = declaringModule ?? throw new ArgumentNullException(nameof(declaringModule));
            _builtinTypeId = builtinTypeId;
        }

        #region IPythonType
        public virtual string Name => InnerType.Name;
        public IPythonModule DeclaringModule { get; }
        public virtual string Documentation => InnerType.Documentation;
        public virtual  BuiltinTypeId TypeId => InnerType.TypeId;
        public virtual PythonMemberType MemberType => InnerType.MemberType;
        public virtual  bool IsBuiltin => InnerType.IsBuiltin;
        public virtual bool IsAbstract => InnerType.IsAbstract;
        public virtual bool IsSpecialized => InnerType.IsSpecialized;

        public virtual IMember CreateInstance(string typeName, IArgumentSet args)
            => IsAbstract ? null : InnerType.CreateInstance(typeName, args);
        public virtual IMember Call(IPythonInstance instance, string memberName, IArgumentSet args) 
            => InnerType.Call(instance, memberName, args);
        public virtual IMember Index(IPythonInstance instance, IArgumentSet args) 
            => InnerType.Index(instance, args);
        #endregion

        #region ILocatedMember
        public Location Location => InnerType?.Location ?? default;
        public LocationInfo Definition => InnerType?.Definition ?? LocationInfo.Empty;
        public IReadOnlyList<LocationInfo> References => InnerType?.References ?? Array.Empty<LocationInfo>();
        public void AddReference(Location location) => InnerType?.AddReference(location);
        public void RemoveReferences(IPythonModule module) => InnerType?.RemoveReferences(module);
        #endregion

        #region IMemberContainer
        public virtual IMember GetMember(string name) => InnerType.GetMember(name);
        public virtual IEnumerable<string> GetMemberNames() => InnerType.GetMemberNames();
        #endregion

        protected IMember UnknownType => DeclaringModule.Interpreter.UnknownType;

        public override bool Equals(object obj)
            => obj is IPythonType pt && (PythonTypeComparer.Instance.Equals(pt, this) || PythonTypeComparer.Instance.Equals(pt, InnerType));
        public override int GetHashCode() => PythonTypeComparer.Instance.GetHashCode(this);
    }
}
