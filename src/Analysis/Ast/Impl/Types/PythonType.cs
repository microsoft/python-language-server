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
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core.Diagnostics;

namespace Microsoft.Python.Analysis.Types {
    [DebuggerDisplay("{Name}")]
    internal class PythonType : LocatedMember, IPythonType {//, IEquatable<IPythonType> {
        private readonly object _lock = new object();
        private readonly string _name;
        private Dictionary<string, IMember> _members;
        private BuiltinTypeId _typeId;
        private bool _readonly;

        protected IReadOnlyDictionary<string, IMember> Members => WritableMembers;

        private Dictionary<string, IMember> WritableMembers =>
            _members ?? (_members = new Dictionary<string, IMember>());

        public PythonType(
            string name,
            Location location,
            string documentation,
            BuiltinTypeId typeId = BuiltinTypeId.Unknown
        ) : this(name, location, typeId) {
            Documentation = documentation;
        }

        private PythonType(string name, Location location, BuiltinTypeId typeId) : base(location) {
            Check.ArgumentNotNull(nameof(location), location.Module);
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _typeId = typeId;
        }

        #region ILocatedMember
        public override PythonMemberType MemberType => _typeId.GetMemberId();
        #endregion

        #region IPythonType

        public virtual string Name => TypeId == BuiltinTypeId.Ellipsis ? "..." : _name;

        public virtual string QualifiedName
            => DeclaringModule.ModuleType == ModuleType.Builtins
                       ? TypeId == BuiltinTypeId.Ellipsis ? "ellipsis" : Name
                       : this.GetQualifiedName();

        public virtual string Documentation { get; private set; }
        public virtual BuiltinTypeId TypeId => _typeId;
        public bool IsBuiltin => DeclaringModule == null || DeclaringModule is IBuiltinsPythonModule;
        public virtual bool IsAbstract => false;
        public virtual bool IsSpecialized => false;

        /// <summary>
        /// Create instance of the type, if any.
        /// </summary>
        /// <param name="typeName">Name of the type. Used in specialization scenarios
        /// where constructor may want to create specialized type.</param>
        /// <param name="args">Any custom arguments required to create the instance.</param>
        public virtual IMember CreateInstance(string typeName, IArgumentSet args) => new PythonInstance(this);

        /// <summary>
        /// Invokes method or property on the specified instance.
        /// </summary>
        /// <param name="instance">Instance of the type.</param>
        /// <param name="memberName">Member name to call, if applicable.</param>
        /// <param name="argSet">Call arguments.</param>
        public virtual IMember Call(IPythonInstance instance, string memberName, IArgumentSet argSet)
            => instance?.Call(memberName, argSet) ?? UnknownType;

        /// <summary>
        /// Invokes indexer on the specified instance.
        /// </summary>
        /// <param name="instance">Instance of the type.</param>
        /// <param name="index">Index arguments.</param>
        public virtual IMember Index(IPythonInstance instance, IArgumentSet args) => instance?.Index(args) ?? UnknownType;
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

        internal virtual void SetDocumentation(string documentation) => Documentation = documentation;

        internal void AddMembers(IEnumerable<IVariable> variables, bool overwrite) {
            lock (_lock) {
                if (!_readonly) {
                    foreach (var v in variables.Where(m => overwrite || !Members.ContainsKey(m.Name))) {
                        // If variable holds function or a class, use value as member. 
                        // If it holds an instance, use the variable itself (i.e. it is a data member).
                        WritableMembers[v.Name] = v.Value;
                    }
                }
            }
        }

        internal void AddMembers(IEnumerable<KeyValuePair<string, IMember>> members, bool overwrite) {
            lock (_lock) {
                if (!_readonly) {
                    foreach (var kv in members.Where(m => overwrite || !Members.ContainsKey(m.Key))) {
                        WritableMembers[kv.Key] = kv.Value;
                    }
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
                if (!_readonly) {
                    if (overwrite || !Members.ContainsKey(name)) {
                        WritableMembers[name] = member;
                    }
                }
                return member;
            }
        }

        internal void MakeReadOnly() => _readonly = true;

        internal bool IsHidden => ContainsMember("__hidden__");
        protected bool ContainsMember(string name) => Members.ContainsKey(name);
        protected IMember UnknownType => DeclaringModule.Interpreter.UnknownType;

        //public bool Equals(IPythonType other) => PythonTypeComparer.Instance.Equals(this, other);

        //public override bool Equals(object obj)
        //    => obj is IPythonType pt && PythonTypeComparer.Instance.Equals(this, pt);
        //public override int GetHashCode() => 0;
    }
}
