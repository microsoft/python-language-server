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
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonType : IPythonType2, IMemberContainer, ILocatedMember, IHasQualifiedName {
        private static readonly IPythonModule NoDeclModule = new AstPythonModule();

        private Dictionary<string, IMember> _members;
        private readonly string _name;
        private IReadOnlyList<IPythonType> _mro;
        private AsyncLocal<bool> _isProcessing = new AsyncLocal<bool>();

        protected Dictionary<string, IMember> Members =>
            _members ?? (_members = new Dictionary<string, IMember>());

        public AstPythonType(string name): this(name, new Dictionary<string, IMember>(), Array.Empty<ILocationInfo>()) { }

        public AstPythonType(
            string name,
            PythonAst ast,
            IPythonModule declModule,
            int startIndex,
            string doc,
            ILocationInfo loc,
            bool isClass = false
        ) {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            Documentation = doc;
            DeclaringModule = declModule ?? throw new ArgumentNullException(nameof(declModule));
            Locations = loc != null ? new[] { loc } : Array.Empty<ILocationInfo>();
            StartIndex = startIndex;
            IsClass = isClass;
        }

        private AstPythonType(string name, Dictionary<string, IMember> members, IEnumerable<ILocationInfo> locations) {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _members = members;
            _mro = Array.Empty<IPythonType>();
            DeclaringModule = NoDeclModule;
            Locations = locations;
        }

        internal void AddMembers(IEnumerable<KeyValuePair<string, IMember>> members, bool overwrite) {
            lock (Members) {
                foreach (var kv in members) {
                    if (!overwrite) {
                        if (Members.TryGetValue(kv.Key, out var existing)) {
                            continue;
                        }
                    }
                    Members[kv.Key] = kv.Value;
                }
            }
        }

        internal void SetBases(IPythonInterpreter interpreter, IEnumerable<IPythonType> bases) {
            lock (Members) {
                if (Bases != null) {
                    return; // Already set
                }

                Bases = bases.MaybeEnumerate().ToArray();
                if (Bases.Count > 0) {
                    Members["__base__"] = Bases[0];
                }

                Members["__bases__"] = new AstPythonSequence(
                    interpreter?.GetBuiltinType(BuiltinTypeId.Tuple),
                    DeclaringModule,
                    Bases,
                    interpreter?.GetBuiltinType(BuiltinTypeId.TupleIterator)
                );
            }
        }

        public IReadOnlyList<IPythonType> Mro {
            get {
                lock (Members) {
                    if (_mro != null) {
                        return _mro;
                    }
                    if (Bases == null) {
                        //Debug.Fail("Accessing Mro before SetBases has been called");
                        return new IPythonType[] { this };
                    }
                    _mro = new IPythonType[] { this };
                    _mro = CalculateMro(this);
                    return _mro;
                }
            }
        }

        internal static IReadOnlyList<IPythonType> CalculateMro(IPythonType cls, HashSet<IPythonType> recursionProtection = null) {
            if (cls == null) {
                return Array.Empty<IPythonType>();
            }
            if (recursionProtection == null) {
                recursionProtection = new HashSet<IPythonType>();
            }
            if (!recursionProtection.Add(cls)) {
                return Array.Empty<IPythonType>();
            }
            try {
                var mergeList = new List<List<IPythonType>> { new List<IPythonType>() };
                var finalMro = new List<IPythonType> { cls };

                var bases = (cls as AstPythonType)?.Bases ??
                    (cls.GetMember(null, "__bases__") as IPythonSequenceType)?.IndexTypes ??
                    Array.Empty<IPythonType>();

                foreach (var b in bases) {
                    var b_mro = new List<IPythonType>();
                    b_mro.AddRange(CalculateMro(b, recursionProtection));
                    mergeList.Add(b_mro);
                }

                while (mergeList.Any()) {
                    // Next candidate is the first head that does not appear in
                    // any other tails.
                    var nextInMro = mergeList.FirstOrDefault(mro => {
                        var m = mro.FirstOrDefault();
                        return m != null && !mergeList.Any(m2 => m2.Skip(1).Contains(m));
                    })?.FirstOrDefault();

                    if (nextInMro == null) {
                        // MRO is invalid, so return just this class
                        return new IPythonType[] { cls };
                    }

                    finalMro.Add(nextInMro);

                    // Remove all instances of that class from potentially being returned again
                    foreach (var mro in mergeList) {
                        mro.RemoveAll(ns => ns == nextInMro);
                    }

                    // Remove all lists that are now empty.
                    mergeList.RemoveAll(mro => !mro.Any());
                }

                return finalMro;
            } finally {
                recursionProtection.Remove(cls);
            }
        }

        public string Name {
            get {
                lock (Members) {
                    IMember nameMember;
                    if (Members.TryGetValue("__name__", out nameMember) && nameMember is AstPythonStringLiteral lit) {
                        return lit.Value;
                    }
                }
                return _name;
            }
        }
        public string Documentation { get; }
        public IPythonModule DeclaringModule { get; }
        public IReadOnlyList<IPythonType> Bases { get; private set; }
        public virtual bool IsBuiltin => false;
        public PythonMemberType MemberType => PythonMemberType.Class;
        public virtual BuiltinTypeId TypeId => BuiltinTypeId.Type;
        public virtual bool IsClass { get; }

        /// <summary>
        /// The start index of this class. Used to disambiguate multiple
        /// class definitions with the same name in the same file.
        /// </summary>
        public int StartIndex { get; }

        public IEnumerable<ILocationInfo> Locations { get; }

        public string FullyQualifiedName => FullyQualifiedNamePair.CombineNames();
        public KeyValuePair<string, string> FullyQualifiedNamePair => new KeyValuePair<string, string>(DeclaringModule.Name, Name);

        public IMember GetMember(IModuleContext context, string name) {
            IMember member;
            lock (Members) {
                if (Members.TryGetValue(name, out member)) {
                    return member;
                }

                // Special case names that we want to add to our own Members dict
                switch (name) {
                    case "__mro__":
                        member = Members[name] = new AstPythonSequence(
                            (context as IPythonInterpreter)?.GetBuiltinType(BuiltinTypeId.Tuple),
                            DeclaringModule,
                            Mro,
                            (context as IPythonInterpreter)?.GetBuiltinType(BuiltinTypeId.TupleIterator)
                        );
                        return member;
                }
            }
            if (Push()) {
                try {
                    foreach (var m in Mro.Reverse()) {
                        if (m == this) {
                            return member;
                        }
                        member = member ?? m.GetMember(context, name);
                    }
                } finally {
                    Pop();
                }
            }
            return null;
        }

        private bool Push() => _isProcessing.Value ? false : (_isProcessing.Value = true);
        private void Pop() => _isProcessing.Value = false;

        public IPythonFunction GetConstructors() => GetMember(null, "__init__") as IPythonFunction;

        public IEnumerable<string> GetMemberNames(IModuleContext moduleContext) {
            var names = new HashSet<string>();
            lock (Members) {
                names.UnionWith(Members.Keys);
            }

            foreach (var m in Mro.Skip(1)) {
                names.UnionWith(m.GetMemberNames(moduleContext));
            }

            return names;
        }
    }
}
