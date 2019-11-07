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
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Types.Collections;
using Microsoft.Python.Analysis.Utilities;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Analysis.Values.Collections;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Types {
    [DebuggerDisplay("Class {" + nameof(Name) + "}")]
    internal partial class PythonClassType : PythonType, IPythonClassType, IEquatable<IPythonClassType> {
        internal enum ClassDocumentationSource {
            Class,
            Init,
            Base
        }
        private static readonly string[] _classMethods = { "mro", "__dict__", @"__weakref__" };

        private readonly ReentrancyGuard<IPythonClassType> _memberGuard = new ReentrancyGuard<IPythonClassType>();
        private readonly object _membersLock = new object();

        private List<IPythonType> _bases;
        private IReadOnlyList<IPythonType> _mro;
        private string _documentation;

        // For tests
        internal PythonClassType(string name, Location location)
            : base(name, location, string.Empty, BuiltinTypeId.Type) {
        }

        public PythonClassType(
            ClassDefinition classDefinition,
            IPythonType declaringType,
            Location location,
            BuiltinTypeId builtinTypeId = BuiltinTypeId.Type
        ) : base(classDefinition.Name, location, classDefinition.GetDocumentation(), builtinTypeId) {
            location.Module.AddAstNode(this, classDefinition);
            DeclaringType = declaringType;
        }

        #region IPythonType
        /// <summary>
        /// If class has generic type parameters, returns that form, e.g 'A[T1, int, ...]', otherwise returns base, e.g 'A'
        /// </summary>
        public override string Name => _nameWithParameters ?? base.Name;
        public override string QualifiedName => this.GetQualifiedName(_qualifiedNameWithParameters);
        public override PythonMemberType MemberType => PythonMemberType.Class;

        public override IEnumerable<string> GetMemberNames() {
            lock (_membersLock) {
                var names = new HashSet<string>(Members.Keys);
                foreach (var m in Mro.Skip(1)) {
                    names.UnionWith(m.GetMemberNames());
                }
                return DeclaringModule.Interpreter.LanguageVersion.Is3x() ? names.Concat(_classMethods).Distinct() : names;
            }
        }

        public override IMember GetMember(string name) {
            lock (_membersLock) {
                if (Members.TryGetValue(name, out var member)) {
                    return member;
                }
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
                    return Mro.Skip(1).Select(c => c.GetMember(name)).ExcludeDefault().FirstOrDefault();
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
                        _documentation = Bases
                            .FirstOrDefault(b => b != o && !(b is IGenericClassBase) && !string.IsNullOrEmpty(b?.Documentation))?
                            .Documentation;
                        DocumentationSource = ClassDocumentationSource.Base;
                    }
                }
                return _documentation;
            }
        }

        // Constructor call
        public override IMember CreateInstance(IArgumentSet args) {
            var builtins = DeclaringModule.Interpreter.ModuleResolution.BuiltinsModule;
            // Specializations
            switch (Name) {
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
            // Metaclasses return type, not instance.
            if (Bases.MaybeEnumerate().Any(b => b.Name == "type" && b.DeclaringModule.ModuleType == ModuleType.Builtins)) {
                return this;
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

        #region IPythonClassMember
        public IPythonType DeclaringType { get; }
        #endregion

        #region IPythonClass
        public ClassDefinition ClassDefinition => DeclaringModule.GetAstNode<ClassDefinition>(this);
        public IReadOnlyList<IPythonType> Bases {
            get {
                lock(_membersLock) {
                    return _bases?.ToArray();
                }
            }
        }

        public IReadOnlyList<IPythonType> Mro {
            get {
                if (_mro != null) {
                    return _mro;
                }
                if (_bases == null || _bases.Count == 0) {
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
        public virtual IReadOnlyDictionary<string, IPythonType> GenericParameters =>
                _genericParameters ?? EmptyDictionary<string, IPythonType>.Instance;

        #endregion

        internal ClassDocumentationSource DocumentationSource { get; private set; }

        internal override void SetDocumentation(string documentation) {
            _documentation = documentation;
            DocumentationSource = ClassDocumentationSource.Class;
        }

        /// <summary>
        /// Sets class bases. If scope is provided, detects loops in base classes and removes them.
        /// </summary>
        /// <param name="bases">List of base types.</param>
        /// <param name="currentScope">Current scope to look up base types.
        /// Can be null if class is restored from database, in which case
        /// there is no need to try and disambiguate bases.</param>
        internal void SetBases(IEnumerable<IPythonType> bases, IScope currentScope = null) {
            if (_bases != null) {
                return; // Already set
            }

            // Consider
            //    from X import A
            //    class A(A): ...
            bases = DisambiguateBases(bases, currentScope).ToArray();

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
            DecideGeneric();

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

        private IEnumerable<IPythonType> DisambiguateBases(IEnumerable<IPythonType> bases, IScope currentScope) {
            if (bases == null) {
                return Enumerable.Empty<IPythonType>();
            }

            if (currentScope == null) {
                return FilterCircularBases(bases).Where(b => !b.IsUnknown());
            }

            var newBases = new List<IPythonType>();
            foreach (var b in bases) {
                var imported = currentScope.LookupImportedNameInScopes(b.Name, out _);
                if (imported is IPythonType importedType) {
                    // Variable with same name as the base was imported.
                    // If there is also a local declaration, we need to figure out which one wins.
                    var localDeclared = currentScope.LookupNameInScopes(b.Name, out var scope);
                    if (localDeclared != null && scope != null) {
                        // Get locally declared variable, make sure it is a declaration
                        // and that it declared a class.
                        var lv = scope.Variables[b.Name];
                        if (lv.Source != VariableSource.Import && lv.Value is IPythonClassType cls && cls.IsDeclaredAfterOrAt(Location)) {
                            // There is a declaration with the same name, but it appears later in the module. Use the import.
                            if (!importedType.IsUnknown()) {
                                newBases.Add(importedType);
                            }
                            continue;
                        }
                    }
                }

                if (!b.IsUnknown()) {
                    newBases.Add(b);
                }
            }
            return FilterCircularBases(newBases);
        }

        private IEnumerable<IPythonType> FilterCircularBases(IEnumerable<IPythonType> bases) {
            // Inspect each base chain and exclude bases that chains to this class.
            foreach (var b in bases.Where(x => !Equals(x))) {
                if (b is IPythonClassType cls) {
                    var chain = cls.Bases
                        .MaybeEnumerate()
                        .OfType<IPythonClassType>()
                        .SelectMany(x => x.TraverseDepthFirst(c => c.Bases.MaybeEnumerate().OfType<IPythonClassType>()));
                    if (chain.Any(Equals)) {
                        continue;
                    }
                }
                yield return b;
            }
        }
    }
}
