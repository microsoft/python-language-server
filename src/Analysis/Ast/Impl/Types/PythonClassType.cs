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
using System.Threading;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Analysis.Values.Collections;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Types {
    [DebuggerDisplay("Class {Name}")]
    internal sealed class PythonClassType : PythonType, IPythonClassType, IEquatable<IPythonClassType> {
        private readonly object _lock = new object();

        private IReadOnlyList<IPythonType> _mro;
        private readonly AsyncLocal<bool> _isProcessing = new AsyncLocal<bool>();

        // For tests
        internal PythonClassType(string name, IPythonModule declaringModule)
            : base(name, declaringModule, string.Empty, LocationInfo.Empty, BuiltinTypeId.Type) { }

        public PythonClassType(
            ClassDefinition classDefinition,
            IPythonModule declaringModule,
            string documentation,
            LocationInfo loc,
            IPythonInterpreter interpreter,
            BuiltinTypeId builtinTypeId = BuiltinTypeId.Type
        ) : base(classDefinition.Name, declaringModule, documentation, loc, builtinTypeId) {
            ClassDefinition = classDefinition;
        }

        #region IPythonType
        public override PythonMemberType MemberType => PythonMemberType.Class;

        public override IEnumerable<string> GetMemberNames() {
            var names = new HashSet<string>();
            lock (_lock) {
                names.UnionWith(Members.Keys);
            }
            foreach (var m in Mro.Skip(1)) {
                names.UnionWith(m.GetMemberNames());
            }
            return names;
        }

        public override IMember GetMember(string name) {
            IMember member;
            lock (_lock) {
                if (Members.TryGetValue(name, out member)) {
                    return member;
                }

                // Special case names that we want to add to our own Members dict
                switch (name) {
                    case "__mro__":
                        member = AddMember(name, new PythonList(DeclaringModule.Interpreter, LocationInfo.Empty, Mro), true);
                        return member;
                }
            }
            if (Push()) {
                try {
                    foreach (var m in Mro.Reverse()) {
                        if (m == this) {
                            return member;
                        }
                        member = member ?? m.GetMember(name);
                    }
                } finally {
                    Pop();
                }
            }
            return null;
        }

        public override string Documentation {
            get {
                var doc = base.Documentation;
                if (string.IsNullOrEmpty(doc) && Bases != null) {
                    // Try bases
                    doc = Bases.FirstOrDefault(b => !string.IsNullOrEmpty(b?.Documentation))?.Documentation;
                }
                if (string.IsNullOrEmpty(doc)) {
                    doc = GetMember("__init__")?.GetPythonType()?.Documentation;
                }
                return doc;
            }
        }

        // Constructor call
        public override IMember CreateInstance(string typeName, LocationInfo location, IReadOnlyList<object> args) {
            // Specializations
            switch (typeName) {
                case "list":
                    return new PythonList(DeclaringModule.Interpreter, location, args.OfType<IMember>().ToArray());
                case "dict":
                    return new PythonDictionary(DeclaringModule.Interpreter, location, args.OfType<IMember>().FirstOrDefault());
                case "tuple":
                    return new PythonTuple(DeclaringModule.Interpreter, location, args.OfType<IMember>().ToArray());
            }
            return new PythonInstance(this, location);
        }
        #endregion

        #region IPythonClass
        public ClassDefinition ClassDefinition { get; }
        public IReadOnlyList<IPythonType> Bases { get; private set; }

        public IReadOnlyList<IPythonType> Mro {
            get {
                lock (_lock) {
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
        #endregion

        internal void SetBases(IPythonInterpreter interpreter, IEnumerable<IPythonType> bases) {
            lock (_lock) {
                if (Bases != null) {
                    return; // Already set
                }

                Bases = bases.MaybeEnumerate().ToArray();
                if (Bases.Count > 0) {
                    AddMember("__base__", Bases[0], true);
                }

                if (!(DeclaringModule is BuiltinsPythonModule)) {
                    // TODO: If necessary, we can set __bases__ on builtins when the module is fully analyzed.
                    AddMember("__bases__", new PythonList(interpreter, LocationInfo.Empty, Bases), true);
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

                var bases = (cls as PythonClassType)?.Bases;
                if (bases == null) {
                    var members = (cls.GetMember("__bases__") as IPythonSequence)?.Contents ?? Array.Empty<IMember>();
                    bases = members.Select(m => m.GetPythonType()).ToArray();
                }

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
                        return new[] { cls };
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

        private bool Push() => !_isProcessing.Value && (_isProcessing.Value = true);
        private void Pop() => _isProcessing.Value = false;
        public bool Equals(IPythonClassType other) 
            => Name == other?.Name && DeclaringModule.Equals(other?.DeclaringModule);
    }
}
