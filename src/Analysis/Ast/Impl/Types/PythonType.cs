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
        private Func<string, string> _documentationProvider;
        private Func<string, LocationInfo> _locationProvider;
        private Dictionary<string, IMember> _members;
        private BuiltinTypeId _typeId;

        protected IReadOnlyDictionary<string, IMember> Members => WritableMembers;

        private Dictionary<string, IMember> WritableMembers =>
            _members ?? (_members = new Dictionary<string, IMember>());

        public PythonType(
            string name,
            IPythonModule declaringModule,
            string documentation,
            LocationInfo location,
            BuiltinTypeId typeId = BuiltinTypeId.Unknown
        ) : this(name, declaringModule, _ => documentation, _ => location ?? LocationInfo.Empty, typeId) { }

        public PythonType(
            string name,
            IPythonModule declaringModule,
            Func<string, string> documentationProvider,
            Func<string, LocationInfo> locationProvider,
            BuiltinTypeId typeId = BuiltinTypeId.Unknown
        ) : this(name, typeId) {
            DeclaringModule = declaringModule;
            _documentationProvider = documentationProvider;
            _locationProvider = locationProvider;
        }

        public PythonType(string name, BuiltinTypeId typeId) {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _typeId = typeId;
        }

        #region IPythonType
        public virtual string Name { get; }
        public virtual string Documentation => _documentationProvider?.Invoke(Name);
        public IPythonModule DeclaringModule { get; }
        public virtual PythonMemberType MemberType => _typeId.GetMemberId();
        public virtual BuiltinTypeId TypeId => _typeId;
        public bool IsBuiltin => DeclaringModule == null || DeclaringModule is IBuiltinsPythonModule;
        public virtual bool IsAbstract => false;

        /// <summary>
        /// Create instance of the type, if any.
        /// </summary>
        /// <param name="location">Instance location</param>
        /// <param name="args">Any custom arguments required to create the instance.</param>
        public virtual IMember CreateInstance(LocationInfo location, IReadOnlyList<object> args)
            => new PythonInstance(this, location);

        /// <summary>
        /// Invokes method or property on the specified instance.
        /// </summary>
        /// <param name="instance">Instance of the type.</param>
        /// <param name="memberName">Method name.</param>
        /// <param name="args">Call arguments.</param>
        public virtual IMember Call(IPythonInstance instance, string memberName, IReadOnlyList<object> args) 
            => instance.Call(memberName, args);

        /// <summary>
        /// Invokes indexer on the specified instance.
        /// </summary>
        /// <param name="instance">Instance of the type.</param>
        /// <param name="index">Index arguments.</param>
        public virtual IMember Index(IPythonInstance instance, object index) => instance.Index(index);
        #endregion

        #region ILocatedMember
        public virtual LocationInfo Location => _locationProvider?.Invoke(Name) ?? LocationInfo.Empty;
        #endregion

        #region IHasQualifiedName
        public virtual string FullyQualifiedName => FullyQualifiedNamePair.CombineNames();
        public virtual KeyValuePair<string, string> FullyQualifiedNamePair
            => new KeyValuePair<string, string>(DeclaringModule?.Name ?? string.Empty, Name);
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

        internal void SetDocumentationProvider(Func<string, string> provider) => _documentationProvider = provider;
        internal void SetLocationProvider(Func<string, LocationInfo> provider) => _locationProvider = provider;

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

        internal void AddMembers(IPythonClassType cls, bool overwrite) {
            if (cls != null) {
                var names = cls.GetMemberNames();
                var members = names.Select(n => new KeyValuePair<string, IMember>(n, cls.GetMember(n)));
                AddMembers(members, overwrite);
            }
        }

        internal IMember AddMember(string name, IMember member, bool overwrite) {
            lock (_lock) {
                if (overwrite || !Members.ContainsKey(name)) {
                    WritableMembers[name] = member;
                }
                return member;
            }
        }

        internal bool IsHidden => ContainsMember("__hidden__");

        protected bool ContainsMember(string name) => Members.ContainsKey(name);
        protected IMember UnknownType => DeclaringModule.Interpreter.UnknownType;
    }
}
