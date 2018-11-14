// Python Tools for Visual Studio
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
using System.Linq;
using System.Threading;
using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonType : IPythonType, ILocatedMember, IHasQualifiedName {
        protected static readonly IPythonModule NoDeclaringModule = new AstPythonModule();

        private readonly string _name;
        private Dictionary<string, IMember> _members;
        private AsyncLocal<bool> _isProcessing = new AsyncLocal<bool>();
        private object _lock = new object();

        protected IReadOnlyDictionary<string, IMember> Members => WritableMembers;

        private Dictionary<string, IMember> WritableMembers =>
            _members ?? (_members = new Dictionary<string, IMember>());

        public AstPythonType(
            string name,
            IPythonModule declaringModule,
            string doc,
            ILocationInfo loc,
            bool isClassFactory = false
        ) : this(name) {
            Documentation = doc;
            DeclaringModule = declaringModule ?? throw new ArgumentNullException(nameof(declaringModule));
            Locations = loc != null ? new[] { loc } : Array.Empty<ILocationInfo>();
            IsClassFactory = isClassFactory;
        }

        public AstPythonType(string name) {
            _name = name ?? throw new ArgumentNullException(nameof(name));
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
        public IPythonModule DeclaringModule { get; } = NoDeclaringModule;
        public virtual PythonMemberType MemberType => PythonMemberType.Unknown;
        public virtual BuiltinTypeId TypeId => BuiltinTypeId.Type;
        public virtual bool IsBuiltIn => false;
        public virtual IPythonFunction GetConstructors() => null;
        public bool IsClassFactory { get; }

        #endregion

        #region ILocatedMember
        public virtual IEnumerable<ILocationInfo> Locations { get; } = Array.Empty<ILocationInfo>();
        #endregion

        #region IHasQualifiedName
        public virtual string FullyQualifiedName => FullyQualifiedNamePair.CombineNames();
        public virtual KeyValuePair<string, string> FullyQualifiedNamePair => new KeyValuePair<string, string>(DeclaringModule.Name, Name);
        #endregion

        #region IMemberContainer
        public virtual IMember GetMember(IModuleContext context, string name) => Members.TryGetValue(name, out var member) ? member : null;
        public virtual IEnumerable<string> GetMemberNames(IModuleContext moduleContext) => Members.Keys;
        #endregion

        internal void AddMembers(IEnumerable<KeyValuePair<string, IMember>> members, bool overwrite) {
            lock (_lock) {
                foreach (var kv in members.Where(m => overwrite || !Members.ContainsKey(m.Key))) {
                    WritableMembers[kv.Key] = kv.Value;
                }
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

        protected bool ContainsMember(string name) => Members.ContainsKey(name);
    }
}
