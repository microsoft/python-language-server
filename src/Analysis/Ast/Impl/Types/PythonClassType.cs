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
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Types.Collections;
using Microsoft.Python.Analysis.Utilities;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Analysis.Values.Collections;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Types {
    [DebuggerDisplay("Class {Name}")]
    internal class PythonClassType : PythonType, IPythonClassType, IPythonTemplateType, IEquatable<IPythonClassType> {
        private static readonly string[] _classMethods = { "mro", "__dict__", @"__weakref__" };
        private readonly object _lock = new object();
        private readonly AsyncLocal<IPythonClassType> _processing = new AsyncLocal<IPythonClassType>();
        private List<IPythonType> _bases;
        private IReadOnlyList<IPythonType> _mro;
        private Dictionary<string, IPythonType> _genericParameters;

        // For tests
        internal PythonClassType(string name, IPythonModule declaringModule, LocationInfo location = null)
            : base(name, declaringModule, string.Empty, location ?? LocationInfo.Empty, BuiltinTypeId.Type) { }

        public PythonClassType(
            ClassDefinition classDefinition,
            IPythonModule declaringModule,
            LocationInfo location,
            BuiltinTypeId builtinTypeId = BuiltinTypeId.Type
        ) : base(classDefinition.Name, declaringModule, classDefinition.GetDocumentation(), location, builtinTypeId) {
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
            return DeclaringModule.Interpreter.LanguageVersion.Is3x() ? names.Concat(_classMethods).Distinct() : names;
        }

        public override IMember GetMember(string name) {
            IMember member;
            lock (_lock) {
                if (Members.TryGetValue(name, out member)) {
                    return member;
                }

                // Special case names that we want to add to our own Members dict
                var is3x = DeclaringModule.Interpreter.LanguageVersion.Is3x();
                switch (name) {
                    case "__mro__":
                    case "mro":
                        return is3x ? PythonCollectionType.CreateList(DeclaringModule.Interpreter, LocationInfo.Empty, Mro) : UnknownType;
                    case "__dict__":
                        return is3x ? DeclaringModule.Interpreter.GetBuiltinType(BuiltinTypeId.Dict) : UnknownType;
                    case @"__weakref__":
                        return is3x ? DeclaringModule.Interpreter.GetBuiltinType(BuiltinTypeId.Object) : UnknownType;
                }
            }
            if (Push(this)) {
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
                // Try doc from the type (class definition AST node).
                var doc = ClassDefinition.GetDocumentation();
                if (string.IsNullOrEmpty(doc)) {
                    doc = base.Documentation;
                }
                // Try docs __init__.
                if (string.IsNullOrEmpty(doc)) {
                    // Don't call GetMember("__init__").Documentation or you stack overflow.
                    doc = (GetMember("__init__") as IPythonFunctionType)?.FunctionDefinition?.Documentation;
                }
                // Try bases.
                if (string.IsNullOrEmpty(doc) && Bases != null) {
                    doc = Bases.FirstOrDefault(b => !string.IsNullOrEmpty(b?.Documentation))?.Documentation;
                }
                return doc;
            }
        }

        // Constructor call
        public override IMember CreateInstance(string typeName, LocationInfo location, IArgumentSet args) {
            // Specializations
            switch (typeName) {
                case "list":
                    return PythonCollectionType.CreateList(DeclaringModule.Interpreter, location, args);
                case "dict": {
                        // self, then contents
                        var contents = args.Values<IMember>().Skip(1).FirstOrDefault();
                        return new PythonDictionary(DeclaringModule.Interpreter, location, contents);
                    }
                case "tuple": {
                        var contents = args.Values<IMember>();
                        return PythonCollectionType.CreateTuple(DeclaringModule.Interpreter, location, contents);
                    }
            }
            return new PythonInstance(this, location);
        }

        public override IMember Index(IPythonInstance instance, object index) {
            var defaultReturn = base.Index(instance, index);
            var fromBases = Bases
                .MaybeEnumerate()
                .Select(b => b.Index(instance, index))
                .Except(new[] { defaultReturn, UnknownType })
                .FirstOrDefault();

            return fromBases ?? defaultReturn;
        }

        #endregion

        #region IPythonClass
        public ClassDefinition ClassDefinition { get; }
        public IReadOnlyList<IPythonType> Bases => _bases;

        public IReadOnlyList<IPythonType> Mro {
            get {
                lock (_lock) {
                    if (_mro != null) {
                        return _mro;
                    }
                    if (_bases == null) {
                        return new IPythonType[] { this };
                    }
                    _mro = new IPythonType[] { this };
                    _mro = CalculateMro(this);
                    return _mro;
                }
            }
        }

        public IReadOnlyDictionary<string, IPythonType> GenericParameters
            => _genericParameters ?? EmptyDictionary<string, IPythonType>.Instance;
        #endregion

        internal void SetBases(IEnumerable<IPythonType> bases) {
            lock (_lock) {
                if (_bases != null) {
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

                AddMember("__bases__", PythonCollectionType.CreateList(DeclaringModule.Interpreter, LocationInfo.Empty, _bases), true);
            }
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

        private bool Push(IPythonClassType cls) {
            if (_processing.Value == null) {
                _processing.Value = cls;
                return true;
            }
            return false;
        }

        private void Pop() => _processing.Value = null;
        public bool Equals(IPythonClassType other)
            => Name == other?.Name && DeclaringModule.Equals(other?.DeclaringModule);

        public IPythonType CreateSpecificType(IArgumentSet args, IPythonModule declaringModule, LocationInfo location = null) {
            location = location ?? LocationInfo.Empty;
            // Get declared generic parameters of the class, i.e. list of Ts in Generic[T1, T2, ...]
            var genericClassParameters = Bases.OfType<IGenericClassParameter>().ToArray();
            // Optimistically use the first one
            // TODO: handle optional generics as class A(Generic[_T1], Optional[Generic[_T2]])
            var genericClassParameter = genericClassParameters.FirstOrDefault();

            // Create map of names listed in Generic[...] in the class definition.
            // We will be filling the map with specific types, if any provided.
            var genericTypeDefinitions = genericClassParameter?.TypeDefinitions ?? Array.Empty<IGenericTypeDefinition>();
            var genericClassTypeParameters = genericTypeDefinitions.ToDictionary(td => td.Name, td => td);

            var specificClassTypeParameters = new Dictionary<string, IPythonType>();
            var newBases = new List<IPythonType>();

            // Arguments passed are those of __init__ or it is a copy constructor.
            // They do not necessarily match all of the declared generic parameters.
            // Some generic parameters may be used to specify method return types or
            // method arguments and do not appear in the constructor argument list.
            // Figure out whatever specific types we can from the arguments.
            foreach (var a in args.Arguments) {
                // The argument may either match generic type definition of be a specific type
                // created off generic type. Consider '__init__(self, v: _T)' and
                // 'class A(Generic[K, V], Mapping[K, V])'.
                if (a.Type is IGenericTypeDefinition argTypeDefinition) {
                    // Parameter is annotated as generic type definition. Check if its generic type
                    // name matches any of the generic class parameters. I.e. if there is
                    // an argument like 'v: _T' we need to check if class has matching Generic[_T].
                    if (genericClassTypeParameters.ContainsKey(argTypeDefinition.Name)) {
                        // TODO: Check if specific type matches generic type definition constraints and report mismatches.
                        // Assign specific type.
                        if (a.Value is IMember m && m.GetPythonType() is IPythonType pt) {
                            specificClassTypeParameters[argTypeDefinition.Name] = pt;
                        } else {
                            // TODO: report supplied parameter is not a type.
                        }
                    } else {
                        // TODO: report generic parameter name mismatch.
                    }
                } else if (a.Value is IMember member && !member.GetPythonType().IsUnknown()) {
                    var type = member.GetPythonType();
                    // Type may be a specific type created off generic or just a type
                    // for the copy constructor. Consider 'class A(Generic[K, V], Mapping[K, V])'
                    // constructed as 'd = {1:'a', 2:'b'}; A(d)'. Here we look through bases
                    // and see if any matches the builtin type id. For example, Mapping or Dict
                    // will have BultinTypeId.Dict and we can figure out specific types from
                    // the content of the collection.
                    var b = _bases.OfType<IGenericType>().FirstOrDefault(x => x.TypeId == type.TypeId);
                    if (b != null && b.Parameters.Count > 0) {
                        newBases.Add(type);
                        // Optimistically assign argument types if they match.
                        // Handle common cases directly.
                        GetSpecificTypeFromArgumentValue(b, a.Value, specificClassTypeParameters);
                    } else {
                        // Any regular base match?
                        if (_bases.Any(x => x.TypeId == type.TypeId)) {
                            newBases.Add(type);
                        }
                        // Just match passed types to generic parameters in order
                        for (var i = 0; i < Math.Min(genericTypeDefinitions.Count, args.Arguments.Count); i++) {
                            if (args.Arguments[i].Value is IPythonType t) {
                                specificClassTypeParameters[genericTypeDefinitions[i].Name] = t;
                            }
                        }
                    }
                }
            }

            // Define specific type in the original order
            var specificTypes = genericTypeDefinitions
                .Select(p => specificClassTypeParameters.TryGetValue(p.Name, out var v) ? v : null)
                .ExcludeDefault()
                .ToArray();

            var specificName = CodeFormatter.FormatSequence(Name, '[', specificTypes);
            var classType = new PythonClassType(specificName, declaringModule);

            // Methods returning generic types need to know how to match generic
            // parameter name to the actual supplied type.
            StoreGenericParameters(classType, genericTypeDefinitions.ToArray(), specificTypes);

            // Prevent reentrancy when resolving generic class where
            // method may be returning instance of type of the same class.
            if (!Push(classType)) {
                return _processing.Value;
            }

            try {
                // Create specific bases since we may have generic types there.
                // Match generic parameter names to base type parameter names.
                // Consider 'class A(Generic[T], B[T], C[E]): ...'
                var genericTypeBases = Bases.Except(genericClassParameters).OfType<IGenericType>().ToArray();
                // Start with regular types, then add specific types for all generic types.
                var bases = Bases.Except(genericTypeBases).Except(genericClassParameters).ToList();

                // Create specific types for generic type bases
                // it for generic types but not Generic[T, ...] itself.
                foreach (var gt in genericTypeBases) {
                    var st = gt.Parameters
                        .Select(p => classType.GenericParameters.TryGetValue(p.Name, out var t) ? t : null)
                        .Where(p => !p.IsUnknown())
                        .ToArray();
                    if (st.Length > 0) {
                        var type = gt.CreateSpecificType(new ArgumentSet(st), classType.DeclaringModule, location);
                        if (!type.IsUnknown()) {
                            bases.Add(type);
                        }
                    }
                }
                // Set specific class bases
                classType.SetBases(bases.Concat(newBases));
                // Transfer members from generic to specific type.
                SetClassMembers(classType, args, declaringModule, location);
            } finally {
                Pop();
            }
            return classType;
        }

        private void StoreGenericParameters(PythonClassType classType, IGenericTypeDefinition[] genericParameters, IPythonType[] specificTypes) {
            classType._genericParameters = new Dictionary<string, IPythonType>();
            for (var i = 0; i < genericParameters.Length; i++) {
                var gb = genericParameters[i];
                classType._genericParameters[gb.Name] = i < specificTypes.Length ? specificTypes[i] : UnknownType.GetPythonType();
            }
        }

        /// <summary>
        /// Given generic type such as Generic[T1, T2, ...] attempts to extract specific types
        /// for its parameters from an argument value. Handles common cases such as dictionary,
        /// list and tuple. Typically used on argument that is passed to the class constructor.
        /// </summary>
        /// <remarks>
        /// Consider 'class A(Generic[K, V], Dict[K, V]): ...' constructed as
        /// 'd = {1:'a', 2:'b'}; A(d). The method extracts types of keys and values from
        /// the instance of the dictionary so concrete class type can be created with
        /// the specific type arguments.
        /// </remarks>
        /// <param name="gt">Generic type (Generic[T1, T2, ...).</param>
        /// <param name="argumentValue">Argument value passed to the class constructor.</param>
        /// <param name="specificTypes">Dictionary or name (T1) to specific type to populate.</param>
        private void GetSpecificTypeFromArgumentValue(IGenericType gt, object argumentValue, IDictionary<string, IPythonType> specificTypes) {
            switch (argumentValue) {
                case IPythonDictionary dict when gt.Parameters.Count == 2:
                    var keyType = dict.Keys.FirstOrDefault()?.GetPythonType();
                    var valueType = dict.Values.FirstOrDefault()?.GetPythonType();
                    if (!keyType.IsUnknown()) {
                        specificTypes[gt.Parameters[0].Name] = keyType;
                    }
                    if (!valueType.IsUnknown()) {
                        specificTypes[gt.Parameters[1].Name] = valueType;
                    }
                    break;
                case IPythonIterable iter when gt.TypeId == BuiltinTypeId.List && gt.Parameters.Count == 1:
                    var itemType = iter.GetIterator().Next.GetPythonType();
                    if (!itemType.IsUnknown()) {
                        specificTypes[gt.Parameters[0].Name] = itemType;
                    }
                    break;
                case IPythonCollection coll when gt.TypeId == BuiltinTypeId.Tuple && gt.Parameters.Count >= 1:
                    var itemTypes = coll.Contents.Select(m => m.GetPythonType()).ToArray();
                    for (var i = 0; i < Math.Min(itemTypes.Length, gt.Parameters.Count); i++) {
                        specificTypes[gt.Parameters[i].Name] = itemTypes[i];
                    }
                    break;
            }
        }

        /// <summary>
        /// Transfers members from generic class to the specific class type
        /// while instantiating specific types for the members.
        /// </summary>
        private void SetClassMembers(PythonClassType classType, IArgumentSet args, IPythonModule declaringModule, LocationInfo location) {
            // Add members from the template class (this one).
            // Members must be clones rather than references since
            // we are going to set specific types on them.
            classType.AddMembers(this, true);

            // Resolve return types of methods, if any were annotated as generics
            var members = classType.GetMemberNames()
                .Except(new[] { "__class__", "__bases__", "__base__" })
                .ToDictionary(n => n, classType.GetMember);

            // Create specific types.
            // Functions handle generics internally upon the call to Call.
            foreach (var m in members) {
                switch (m.Value) {
                    case IPythonTemplateType tt: {
                            var specificType = tt.CreateSpecificType(args, declaringModule, location);
                            classType.AddMember(m.Key, specificType, true);
                            break;
                        }
                    case IPythonInstance inst: {
                            var t = inst.GetPythonType();
                            IPythonType specificType = null;
                            switch (t) {
                                case IPythonTemplateType tt when tt.IsGeneric():
                                    specificType = tt.CreateSpecificType(args, declaringModule, location);
                                    break;
                                case IGenericTypeDefinition gtd:
                                    classType.GenericParameters.TryGetValue(gtd.Name, out specificType);
                                    break;
                            }

                            if (specificType != null) {
                                classType.AddMember(m.Key, new PythonInstance(specificType, location), true);
                            }
                            break;
                        }
                }
            }
        }
    }
}
