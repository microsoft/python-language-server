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
    class AstPythonClass : AstPythonType, IPythonClass {
        protected static readonly IPythonModule NoDeclModule = new AstPythonModule();

        private readonly object _lock = new object();

        private IReadOnlyList<IPythonType> _mro;
        private AsyncLocal<bool> _isProcessing = new AsyncLocal<bool>();

        public AstPythonClass(
            ClassDefinition classDefinition,
            IPythonModule declaringModule,
            string doc,
            ILocationInfo loc,
            BuiltinTypeId builtinTypeId = BuiltinTypeId.Class,
            bool isBuiltIn = false
        ) : base(classDefinition.Name, declaringModule, doc, loc, builtinTypeId, isBuiltIn, false) {
            ClassDefinition = classDefinition;
        }

        internal AstPythonClass(string name) : base(name, BuiltinTypeId.Class, false) { }

        #region IPythonType
        public override PythonMemberType MemberType 
            => TypeId == BuiltinTypeId.Class ? PythonMemberType.Class : base.MemberType;

        public override IMember GetMember(IModuleContext context, string name) {
            IMember member;
            lock (_lock) {
                if (Members.TryGetValue(name, out member)) {
                    return member;
                }

                // Special case names that we want to add to our own Members dict
                switch (name) {
                    case "__mro__":
                        member = AddMember(name, new AstPythonSequence(
                            (context as IPythonInterpreter)?.GetBuiltinType(BuiltinTypeId.Tuple),
                            DeclaringModule,
                            Mro,
                            (context as IPythonInterpreter)?.GetBuiltinType(BuiltinTypeId.TupleIterator)
                        ), true);
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

                AddMember("__bases__", new AstPythonSequence(
                    interpreter?.GetBuiltinType(BuiltinTypeId.Tuple),
                    DeclaringModule,
                    Bases,
                    interpreter?.GetBuiltinType(BuiltinTypeId.TupleIterator)
                ), true);
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

                var bases = (cls as AstPythonClass)?.Bases ??
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

        private bool Push() => _isProcessing.Value ? false : (_isProcessing.Value = true);
        private void Pop() => _isProcessing.Value = false;
    }
}
