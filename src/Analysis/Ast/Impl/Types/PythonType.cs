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

namespace Microsoft.Python.Analysis.Types {
    [DebuggerDisplay("{Name}")]
    internal class PythonType : IPythonType, ILocatedMember, IHasQualifiedName {
        private readonly string _name;
        private readonly object _lock = new object();
        private Dictionary<string, IPythonType> _members;
        private BuiltinTypeId _typeId;

        protected IReadOnlyDictionary<string, IPythonType> Members => WritableMembers;

        private Dictionary<string, IPythonType> WritableMembers =>
            _members ?? (_members = new Dictionary<string, IPythonType>());

        public PythonType(
            string name,
            IPythonModule declaringModule,
            string doc,
            LocationInfo loc,
            BuiltinTypeId typeId = BuiltinTypeId.Unknown,
            bool isTypeFactory = false
        ) : this(name, typeId, isTypeFactory) {
            Documentation = doc;
            DeclaringModule = declaringModule;
            Locations = loc != null ? new[] { loc } : Array.Empty<LocationInfo>();
            IsTypeFactory = isTypeFactory;
        }

        public PythonType(string name, BuiltinTypeId typeId, bool isTypeFactory = false) {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _typeId = typeId == BuiltinTypeId.Unknown && isTypeFactory ? BuiltinTypeId.Type : typeId;
        }

        #region IPythonType
        public virtual string Name {
            get {
                lock (_lock) {
                    return Members.TryGetValue("__name__", out var nameMember) && nameMember is AstPythonStringLiteral lit ? lit.Value : _name;
                }
            }
        }

        public virtual string Documentation { get; }
        public IPythonModule DeclaringModule { get; }
        public virtual PythonMemberType MemberType => _typeId.GetMemberId();
        public virtual BuiltinTypeId TypeId => _typeId;
        public bool IsBuiltin => DeclaringModule == null || DeclaringModule is IBuiltinPythonModule;
        public bool IsTypeFactory { get; }
        public IPythonFunction GetConstructor() => GetMember("__init__") as IPythonFunction;
        #endregion

        #region ILocatedMember
        public virtual IEnumerable<LocationInfo> Locations { get; } = Array.Empty<LocationInfo>();
        #endregion

        #region IHasQualifiedName
        public virtual string FullyQualifiedName => FullyQualifiedNamePair.CombineNames();
        public virtual KeyValuePair<string, string> FullyQualifiedNamePair => new KeyValuePair<string, string>(DeclaringModule.Name, Name);
        #endregion

        #region IMemberContainer
        public virtual IPythonType GetMember(string name) => Members.TryGetValue(name, out var member) ? member : null;
        public virtual IEnumerable<string> GetMemberNames() => Members.Keys;
        #endregion

        internal bool TrySetTypeId(BuiltinTypeId typeId) {
            if (_typeId != BuiltinTypeId.Unknown) {
                return false;
            }
            _typeId = typeId;
            return true;
        }

        internal void AddMembers(IEnumerable<IVariable> variables, bool overwrite) {
            lock (_lock) {
                foreach (var v in variables.Where(m => overwrite || !Members.ContainsKey(m.Name))) {
                    WritableMembers[v.Name] = v.Type;
                }
            }
        }

        internal void AddMembers(IEnumerable<KeyValuePair<string, IPythonType>> members, bool overwrite) {
            lock (_lock) {
                foreach (var kv in members.Where(m => overwrite || !Members.ContainsKey(m.Key))) {
                    WritableMembers[kv.Key] = kv.Value;
                }
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

        /// <summary>
        /// Provides type factory. Similar to __metaclass__ but does not expose full
        /// metaclass functionality. Used in cases when function has to return a class
        /// rather than the class instance. Example: function annotated as '-> Type[T]'
        /// can be called as a T constructor so func() constructs class instance rather than invoking
        /// call on an existing instance. See also collections/namedtuple typing in the Typeshed.
        /// </summary>
        internal PythonType GetTypeFactory() {
            var clone = new PythonType(
                Name,
                DeclaringModule,
                Documentation,
                Locations.FirstOrDefault(),
                TypeId == BuiltinTypeId.Unknown ? BuiltinTypeId.Type : TypeId, 
                true);
            clone.AddMembers(Members, true);
            return clone;
        }

        protected bool ContainsMember(string name) => Members.ContainsKey(name);
    }
}
