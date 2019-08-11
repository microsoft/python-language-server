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
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Types.Collections;
using Microsoft.Python.Analysis.Utilities;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Analysis.Values.Collections;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Types {
    [DebuggerDisplay("Class {Name}")]

    internal partial class PythonClassType : PythonType, IPythonClassType, IGenericType, IEquatable<IPythonClassType> {
        internal enum ClassDocumentationSource
        {
            Class,
            Init,
            Base
        }
        private static readonly string[] _classMethods = { "mro", "__dict__", @"__weakref__" };

        private ReentrancyGuard<IPythonClassType> _memberGuard = new ReentrancyGuard<IPythonClassType>();
        private string _genericName;
        private List<IPythonType> _bases = new List<IPythonType>();
        private IReadOnlyList<IPythonType> _mro;
        private string _documentation;

        // For tests
        internal PythonClassType(string name, Location location)
            : base(name, location, string.Empty, BuiltinTypeId.Type) {
            Check.ArgumentNotNull(nameof(location), location.Module);
        }

        public PythonClassType(
            ClassDefinition classDefinition,
            IPythonType declaringType,
            Location location,
            BuiltinTypeId builtinTypeId = BuiltinTypeId.Type
        ) : base(classDefinition.Name, location, classDefinition.GetDocumentation(), builtinTypeId) {
            Check.ArgumentNotNull(nameof(location), location.Module);
            location.Module.AddAstNode(this, classDefinition);
            DeclaringType = declaringType;
        }

        /// <summary>
        /// If class has generic type parameters, returns that form, e.g 'A[T1, int, ...]', otherwise returns base, e.g 'A'
        /// </summary>
        public override string Name => _genericName ?? base.Name;

        #region IPythonType
        public override PythonMemberType MemberType => PythonMemberType.Class;
        public IPythonType DeclaringType { get; }

        public override IEnumerable<string> GetMemberNames() {
            var names = new HashSet<string>();
            names.UnionWith(Members.Keys);
            foreach (var m in Mro.Skip(1)) {
                names.UnionWith(m.GetMemberNames());
            }
            return DeclaringModule.Interpreter.LanguageVersion.Is3x() ? names.Concat(_classMethods).Distinct() : names;
        }

        public override IMember GetMember(string name) {
            // Push/Pop should be lock protected.
            if (Members.TryGetValue(name, out var member)) {
                return member;
            }

            // Special case names that we want to add to our own Members dict
            var is3x = DeclaringModule.Interpreter.LanguageVersion.Is3x();
            switch (name) {
                case "__mro__":
                case "mro":
                    return is3x ? PythonCollectionType.CreateList(DeclaringModule, Mro) : UnknownType as IMember;
                case "__dict__":
                    return is3x ? DeclaringModule.Interpreter.GetBuiltinType(BuiltinTypeId.Dict) : UnknownType;
                case @"__weakref__":
                    return is3x ? DeclaringModule.Interpreter.GetBuiltinType(BuiltinTypeId.Object) : UnknownType;
            }

            using (_memberGuard.Push(this, out var reentered)) {
                if (!reentered) {
                    foreach (var m in Mro.Reverse()) {
                        if (m == this) {
                            return member;
                        }
                        member = member ?? m.GetMember(name);
                    }
                }
                return null;
            }
        }

        public override string Documentation {
            get {
                if (!string.IsNullOrEmpty(_documentation)) {
                    return _documentation;
                }

                // Make sure we do not cycle through bases back here.
                using (_memberGuard.Push(this, out var reentered)) {
                    if (reentered) {
                        return null;
                    }
                    // Try doc from the type first (class definition AST node).
                    _documentation = base.Documentation;
                    DocumentationSource = ClassDocumentationSource.Class;

                    if (string.IsNullOrEmpty(_documentation)) {
                        // If not present, try docs __init__. IPythonFunctionType handles
                        // __init__ in a special way so there is no danger of call coming
                        // back here and causing stack overflow.
                        _documentation = (GetMember("__init__") as IPythonFunctionType)?.Documentation;
                        DocumentationSource = ClassDocumentationSource.Init;
                    }

                    if (string.IsNullOrEmpty(_documentation) && Bases != null) {
                        // If still not found, try bases. 
                        var o = DeclaringModule.Interpreter.GetBuiltinType(BuiltinTypeId.Object);
                        _documentation = Bases.FirstOrDefault(b => b != o && !string.IsNullOrEmpty(b?.Documentation))
                            ?.Documentation;
                        DocumentationSource = ClassDocumentationSource.Base;
                    }
                }
                return _documentation;
            }
        }

        // Constructor call
        public override IMember CreateInstance(string typeName, IArgumentSet args) {
            var builtins = DeclaringModule.Interpreter.ModuleResolution.BuiltinsModule;
            // Specializations
            switch (typeName) {
                case "list":
                    return PythonCollectionType.CreateList(builtins, args);
                case "dict": {
                        // self, then contents
                        var contents = args.Values<IMember>().Skip(1).FirstOrDefault();
                        return new PythonDictionary(builtins, contents);
                    }
                case "tuple": {
                        var contents = args.Values<IMember>();
                        return PythonCollectionType.CreateTuple(builtins, contents);
                    }
            }
            return new PythonInstance(this);
        }

        public override IMember Index(IPythonInstance instance, IArgumentSet args) {
            var defaultReturn = base.Index(instance, args);
            var fromBases = Bases
                .MaybeEnumerate()
                .Select(b => b.Index(instance, args))
                .Except(new[] { defaultReturn, UnknownType })
                .FirstOrDefault();

            return fromBases ?? defaultReturn;
        }

        #endregion

        #region IPythonClass
        public ClassDefinition ClassDefinition => DeclaringModule.GetAstNode<ClassDefinition>(this);
        public IReadOnlyList<IPythonType> Bases => _bases;

        public IReadOnlyList<IPythonType> Mro {
            get {
                if (_mro != null) {
                    return _mro;
                }
                if (_bases.Count == 0) {
                    return new IPythonType[] { this };
                }
                _mro = new IPythonType[] { this };
                _mro = CalculateMro(this);
                return _mro;
            }
        }

        /// <summary>
        /// Mapping from class generic type parameters to what it was filled in with 
        /// class A(Generic[T, K]): ...
        /// class B(A[int, str]): ...
        /// Has the map {T: int, K: str}
        /// </summary>
        public virtual IReadOnlyDictionary<IGenericTypeParameter, IPythonType> GenericParameters =>
                _genericParameters ?? EmptyDictionary<IGenericTypeParameter, IPythonType>.Instance;

        #endregion

        internal ClassDocumentationSource DocumentationSource { get; private set; }
        internal override void SetDocumentation(string documentation) => _documentation = documentation;

        internal void SetBases(IEnumerable<IPythonType> bases) {
            if (_bases.Count > 0) {
                return; // Already set
            }

            bases = bases != null ? bases.Where(b => !b.GetPythonType().IsUnknown()).ToArray() : Array.Empty<IPythonType>();

            // For Python 3+ attach object as a base class by default except for the object class itself.
            if (DeclaringModule.Interpreter.LanguageVersion.Is3x() && DeclaringModule.ModuleType != ModuleType.Builtins) {
                var objectType = DeclaringModule.Interpreter.GetBuiltinType(BuiltinTypeId.Object);
                // During processing of builtins module some types may not be available yet.
                // Specialization will attach proper base at the end.
                Debug.Assert(!objectType.IsUnknown());
                if (!bases.Any(b => objectType.Equals(b))) {
                    bases = bases.Concat(Enumerable.Repeat(objectType, 1));
                }
            }

            _bases = bases.ToList();
            if (_bases.Count > 0) {
                AddMember("__base__", _bases[0], true);
            }
            // Invalidate MRO
            _mro = null;
            if (DeclaringModule is BuiltinsPythonModule) {
                // TODO: If necessary, we can set __bases__ on builtins when the module is fully analyzed.
                return;
            }

            AddMember("__bases__", PythonCollectionType.CreateList(DeclaringModule.Interpreter.ModuleResolution.BuiltinsModule, _bases), true);
        }

        /// <summary>
        /// Calculates MRO according to https://www.python.org/download/releases/2.3/mro/
        /// </summary>
        internal static IReadOnlyList<IPythonType> CalculateMro(IPythonType type, HashSet<IPythonType> recursionProtection = null) {
            if (type == null) {
                return Array.Empty<IPythonType>();
            }
            recursionProtection = recursionProtection ?? new HashSet<IPythonType>();
            if (!recursionProtection.Add(type)) {
                return Array.Empty<IPythonType>();
            }

            var bases = (type as IPythonClassType)?.Bases;
            if (bases == null) {
                var members = (type.GetMember("__bases__") as IPythonCollection)?.Contents ?? Array.Empty<IMember>();
                bases = members.Select(m => m.GetPythonType()).ToArray();
            }

            try {
                var mergeList = new List<List<IPythonType>> { new List<IPythonType>() };
                var finalMro = new List<IPythonType> { type };

                var mros = bases.Select(b => CalculateMro(b, recursionProtection).ToList());
                mergeList.AddRange(mros);

                while (mergeList.Any()) {
                    // Next candidate is the first head that does not appear in any other tails.
                    var nextInMro = mergeList.FirstOrDefault(mro => {
                        var m = mro.FirstOrDefault();
                        return m != null && !mergeList.Any(m2 => m2.Skip(1).Contains(m));
                    })?.FirstOrDefault();

                    if (nextInMro == null) {
                        // MRO is invalid, so return just this class
                        return new[] { type };
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
                recursionProtection.Remove(type);
            }
        }

        public bool Equals(IPythonClassType other)
            => Name == other?.Name && DeclaringModule.Equals(other?.DeclaringModule);

    }
}
