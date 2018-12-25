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
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Types {
    [DebuggerDisplay("{Name}")]
    internal class PythonType : IPythonType, ILocatedMember, IHasQualifiedName {
        private readonly object _lock = new object();
        private Dictionary<string, IMember> _members;
        private BuiltinTypeId _typeId;

        protected IReadOnlyDictionary<string, IMember> Members => WritableMembers;

        private Dictionary<string, IMember> WritableMembers =>
            _members ?? (_members = new Dictionary<string, IMember>());

        public PythonType(
            string name,
            IPythonModule declaringModule,
            string documentation,
            LocationInfo loc,
            BuiltinTypeId typeId = BuiltinTypeId.Unknown
        ) : this(name, typeId) {
            Documentation = documentation;
            DeclaringModule = declaringModule;
            Location = loc ?? LocationInfo.Empty;
        }

        public PythonType(string name, BuiltinTypeId typeId) {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _typeId = typeId;
        }

        #region IPythonType
        public virtual string Name { get; }
        public virtual string Documentation { get; private set; }
        public IPythonModule DeclaringModule { get; }
        public virtual PythonMemberType MemberType => _typeId.GetMemberId();
        public virtual BuiltinTypeId TypeId => _typeId;
        public bool IsBuiltin => DeclaringModule == null || DeclaringModule is IBuiltinsPythonModule;
        public IPythonFunction GetConstructor() => GetMember("__init__") as IPythonFunction;
        #endregion

        #region ILocatedMember
        public virtual LocationInfo Location { get; } = LocationInfo.Empty;
        #endregion

        #region IHasQualifiedName
        public virtual string FullyQualifiedName => FullyQualifiedNamePair.CombineNames();
        public virtual KeyValuePair<string, string> FullyQualifiedNamePair => new KeyValuePair<string, string>(DeclaringModule.Name, Name);
        #endregion

        #region IMemberContainer
        public virtual IMember GetMember(string name) => Members.TryGetValue(name, out var member) ? member : null;
        public virtual IEnumerable<string> GetMemberNames() => Members.Keys;
        #endregion

        internal bool TrySetTypeId(BuiltinTypeId typeId) {
            if (_typeId != BuiltinTypeId.Unknown) {
                return false;
            }
            _typeId = typeId;
            return true;
        }

        internal void SetDocumentation(string doc) {
            if (string.IsNullOrEmpty(Documentation)) {
                Documentation = doc;
            }
        }

        internal void AddMembers(IEnumerable<IVariable> variables, bool overwrite) {
            lock (_lock) {
                foreach (var v in variables.Where(m => overwrite || !Members.ContainsKey(m.Name))) {
                    WritableMembers[v.Name] = v.Value.GetPythonType();
                }
            }
        }

        internal void AddMembers(IEnumerable<KeyValuePair<string, IMember>> members, bool overwrite) {
            lock (_lock) {
                foreach (var kv in members.Where(m => overwrite || !Members.ContainsKey(m.Key))) {
                    WritableMembers[kv.Key] = kv.Value;
                }
            }
        }

        internal void AddMembers(IPythonClass cls, bool overwrite) {
            if (cls != null) {
                var names = cls.GetMemberNames();
                var members = names.Select(n => new KeyValuePair<string, IMember>(n, cls.GetMember(n)));
                AddMembers(members, overwrite);
            }
        }

        internal IPythonType AddMember(string name, IPythonType member, bool overwrite) {
            lock (_lock) {
                if (overwrite || !Members.ContainsKey(name)) {
                    WritableMembers[name] = member;
                }
                return member;
            }
        }

        internal bool IsHidden => ContainsMember("__hidden__");

        protected bool ContainsMember(string name) => Members.ContainsKey(name);
    }
}
