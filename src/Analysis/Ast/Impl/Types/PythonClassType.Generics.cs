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
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Utilities;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Types {
    internal partial class PythonClassType {
        private readonly object _genericParameterLock = new object();
        private readonly ReentrancyGuard<IPythonClassType> _genericSpecializationGuard = new ReentrancyGuard<IPythonClassType>();
        private readonly ReentrancyGuard<IPythonClassType> _genericResolutionGuard = new ReentrancyGuard<IPythonClassType>();

        private string _nameWithParameters; // Name of the class with generic parameters like abc[int].
        private string _qualifiedNameWithParameters; // Qualified name with qualified parameter names for persistence.

        private Dictionary<string, PythonClassType> _specificTypeCache;
        // Actual assigned parameters for generic class.
        private Dictionary<IGenericTypeParameter, IPythonType> _genericActualParameters;
        // Not yet set generic parameters i.e. TypeVars from Generic[T1, T2, ...].
        private IReadOnlyList<IGenericTypeParameter> _genericParameters = new List<IGenericTypeParameter>();

        #region IGenericType
        /// <summary>
        /// List of unfilled generic type parameters. Represented as entries in the ActualGenericParameters dictionary 
        /// that have both key and value as generic type parameters
        /// e.g
        /// {T, T}
        /// Where T is a generic type parameter that needs to be filled in by the class
        /// </summary>
        public virtual IReadOnlyList<IGenericTypeParameter> GenericParameters => _genericParameters ?? Array.Empty<IGenericTypeParameter>();

        public virtual bool IsGeneric { get; private set; }
        #endregion

        /// <summary>
        /// Given an argument set with types, e.g int, float, str, look at the generic type parameters for the class
        /// and create a new class type with those parameters filled in. Additionally, transmit the specific class
        /// types to bases and recreate them with their specific types.
        /// 
        /// e.g
        /// class A(Generic[T, U]): ...
        /// class B(A[T, str]): ...
        /// 
        /// B[int] defines the type parameter T to be of type int and type parameter U to be type str. 
        /// B[int] inherits from A[int, str] 
        /// </summary>
        public virtual IPythonType CreateSpecificType(IArgumentSet args) {
            lock (_genericParameterLock) {
                var genericTypeParameters = GetTypeParameters();
                var newBases = new List<IPythonType>();

                // Get map of generic type parameter to specific type - fill in what type goes to what
                // type parameter T -> int, U -> str, etc.
                var genericTypeToSpecificType = GetSpecificTypes(args, genericTypeParameters, newBases);

                var classType = new PythonClassType(BaseName, new Location(DeclaringModule));
                classType.SetDocumentation(Documentation);

                // Storing generic parameters allows methods returning generic types 
                // to know what type parameter returns what specific type
                StoreGenericParameters(classType, genericTypeParameters, genericTypeToSpecificType);

                // Set generic name
                classType.SetNames();

                // Locking so threads can only access class after it's been initialized
                // Store generic parameters first so name updates correctly, then check if class type has been cached
                _specificTypeCache = _specificTypeCache ?? new Dictionary<string, PythonClassType>();
                if (_specificTypeCache.TryGetValue(classType.Name, out var cachedType)) {
                    return cachedType;
                }
                _specificTypeCache[classType.Name] = classType;

                // Prevent re-entrancy when resolving generic class where method may be returning instance of type of the same class.
                // e.g
                // class C(Generic[T]): 
                //  def tmp(self):
                //    return C(5)
                // C(5).tmp()
                // We try to resolve generic type when instantiating 'C' but end up resolving it again on 'tmp' method call, looping infinitely
                using (_genericSpecializationGuard.Push(classType, out var reentered)) {
                    if (reentered) {
                        return classType;
                    }

                    // Bases can be null when not set
                    var bases = Bases ?? Array.Empty<IPythonType>();
                    // Get declared generic class parameters, i.e. Generic[T1, T2, ...], Optional[Generic[T1, ...]]
                    var genericClassParameters = bases.OfType<IGenericClassBase>().ToArray();

                    // Get list of bases that are generic but not generic class parameters, e.g A[T], B[T] but not Generic[T1, T2]
                    var genericTypeBases = bases.Except(genericClassParameters).OfType<IGenericType>().Where(g => g.IsGeneric).ToArray();

                    // Removing all generic bases, and will only specialize genericTypeBases, remove generic class parameters entirely
                    // We remove generic class parameters entirely because the type information is now stored in ActualGenericParameters field
                    // We still need generic bases so we can specialize them 
                    var specificBases = bases.Except(genericTypeBases).Except(genericClassParameters).ToList();

                    // Create specific types for generic type bases
                    foreach (var gt in genericTypeBases) {
                        // Look through generic type bases and see if any of their required type parameters
                        // have received a specific type, and if so create specific type
                        var st = gt.GenericParameters
                            .Select(p => classType.ActualGenericParameters.TryGetValue(p, out var t) ? t : null)
                            .Where(p => !p.IsUnknown())
                            .ToArray();
                        if (st.Length > 0) {
                            var type = gt.CreateSpecificType(new ArgumentSet(st, args.Expression, args.Eval));
                            if (!type.IsUnknown()) {
                                specificBases.Add(type);
                            }
                        }
                    }

                    // Set specific class bases 
                    classType.SetBases(specificBases.Concat(newBases));
                    classType.SetGenericParameters();
                    // Transfer members from generic to specific type.
                    SetClassMembers(classType, args);
                }
                return classType;
            }
        }

        /// <summary>
        /// Gets a list of distinct type parameters from bases and the class itself
        /// </summary>
        private IGenericTypeParameter[] GetTypeParameters() {
            // Case when updating with specific type and already has type parameters, return them
            if (!GenericParameters.IsNullOrEmpty()) {
                return GenericParameters.ToArray();
            }

            var bases = Bases ?? Array.Empty<IPythonType>();
            var fromBases = new HashSet<IGenericTypeParameter>();
            var genericClassParameter = bases.OfType<IGenericClassBase>().FirstOrDefault();

            // If Generic[...] is present, ordering of type variables is determined from that
            if (genericClassParameter != null && genericClassParameter.TypeParameters != null) {
                fromBases.UnionWith(genericClassParameter.TypeParameters);
            } else {
                // otherwise look at the generic class bases
                foreach (var gt in bases.OfType<IGenericType>()) {
                    if (gt.GenericParameters != null) {
                        fromBases.UnionWith(gt.GenericParameters);
                    }
                }
            }

            return fromBases.ToArray();
        }

        /// <summary>
        /// Given an argument set, returns a dictionary mapping generic type parameter to the supplied specific 
        /// type from arguments. 
        /// </summary>
        private IReadOnlyDictionary<IGenericTypeParameter, IPythonType> GetSpecificTypes(IArgumentSet args,
            IGenericTypeParameter[] genericTypeParameters,
            List<IPythonType> newBases) {

            // For now, map each type parameter to itself, and we can fill in the value as we go 
            var genericTypeToSpecificType = genericTypeParameters.ToDictionary(gtp => gtp, gtp => gtp as IPythonType);

            // Arguments passed are those of __init__ or copy constructor or index expression A[int, str, ...].
            // The arguments do not necessarily match all of the declared generic parameters.
            // Some generic parameters may be used to specify method return types or
            // method arguments and do not appear in the constructor argument list.
            // We try to figure out whatever specific types we can from the arguments.
            foreach (var arg in args.Arguments) {
                // The argument may either match generic type parameter or be a specific type
                // created off generic type. Consider '__init__(self, v: _T)' and
                // 'class A(Generic[K, V], Mapping[K, V])'.
                if (arg.Type is IGenericTypeParameter argTypeDefinition) {
                    // Parameter is annotated as generic type definition. 
                    // __init__(self, v: _T), v is annotated as a generic type definition
                    // Check if its generic type name matches any of the generic class parameters i.e. if there is
                    // an argument like 'v: _T' we need to check if class has matching Generic[_T] or A[_T] in bases.
                    if (genericTypeToSpecificType.ContainsKey(argTypeDefinition)) {
                        // TODO: Check if specific type matches generic type parameter constraints and report mismatches.
                        // Assign specific type.
                        if (arg.Value is IMember m && m.GetPythonType() is IPythonType pt) {
                            genericTypeToSpecificType[argTypeDefinition] = pt;
                        } else {
                            // TODO: report supplied parameter is not a type.
                        }
                    } else {
                        // TODO: report generic parameter name mismatch.
                    }
                    continue;
                }

                // Don't add generic type parameters to bases 
                if (!(arg.Value is IGenericTypeParameter) && arg.Value is IMember member && !member.GetPythonType().IsUnknown()) {
                    var type = member.GetPythonType();
                    // Type may be a specific type created off generic or just a type
                    // for the copy constructor. Consider 'class A(Generic[K, V], Mapping[K, V])'
                    // constructed as 'd = {1:'a', 2:'b'}; A(d)'. Here we look through bases
                    // and see if any matches the builtin type id. For example, Mapping or Dict
                    // will have BultinTypeId.Dict and we can figure out specific types from
                    // the content of the collection.
                    var b = _bases.OfType<IGenericType>().Where(g => g.IsGeneric).FirstOrDefault(x => x.TypeId == type.TypeId);
                    if (b != null && !b.GenericParameters.IsNullOrEmpty()) {
                        newBases.Add(type);
                        // Optimistically assign argument types if they match.
                        // Handle common cases directly.
                        GetSpecificTypeFromArgumentValue(b, arg.Value, genericTypeToSpecificType);
                        continue;
                    }

                    // Any regular base match?
                    if (_bases.Any(x => x.TypeId == type.TypeId) && !type.Equals(this)) {
                        newBases.Add(type);
                    }
                }
            }

            // For still undefined parameters try matching passed types in order
            for (int i = 0, gtIndex = 0; i < args.Arguments.Count; i++) {
                var arg = args.Arguments[i];

                if (Equals(arg.Type)) {
                    continue; // Typically 'self'.
                }

                if (arg.Value is IMember member) {
                    var type = member.GetPythonType();
                    if (!type.IsUnknown()) {
                        var gtd = gtIndex < genericTypeParameters.Length ? genericTypeParameters[gtIndex] : null;
                        if (gtd != null && genericTypeToSpecificType.TryGetValue(gtd, out var s) && s is IGenericTypeParameter) {
                            genericTypeToSpecificType[gtd] = type;
                        }
                        gtIndex++;
                    }
                }
            }
            return genericTypeToSpecificType;
        }

        /// <summary>
        /// Points the generic type parameter in class type to their corresponding specific type
        /// (or a generic type parameter if no specific type was provided)
        /// </summary>
        internal void StoreGenericParameters(PythonClassType specificClassType, IGenericTypeParameter[] genericParameters, IReadOnlyDictionary<IGenericTypeParameter, IPythonType> genericToSpecificTypes) {
            // copy original generic parameters over and try to fill them in
            specificClassType._genericActualParameters = new Dictionary<IGenericTypeParameter, IPythonType>(ActualGenericParameters.ToDictionary(k => k.Key, k => k.Value));

            // Case when creating a new specific class type
            if (GenericParameters.Count == 0) {
                // Assign class type generic type parameters to specific types 
                foreach (var gb in genericParameters) {
                    specificClassType._genericActualParameters[gb] = genericToSpecificTypes.TryGetValue(gb, out var v) ? v : null;
                }
            } else {
                // When GenericParameters field is not empty then need to update generic parameters field
                foreach (var gp in ActualGenericParameters.Keys) {
                    if (ActualGenericParameters[gp] is IGenericTypeParameter specificType) {
                        // Get unfilled type parameter or type parameter that was filled with another type parameter
                        // and try to fill it in
                        // e.g 
                        // class A(Generic[T]):
                        // class B(A[U])
                        // A has T => U
                        specificClassType._genericActualParameters[gp] = genericToSpecificTypes.TryGetValue(specificType, out var v) ? v : null;
                    }
                }
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
        /// <param name="gt">Generic type (Generic[T1, T2, ...], A[T1, T2, ..], etc.).</param>
        /// <param name="argumentValue">Argument value passed to the class constructor.</param>
        /// <param name="specificTypes">Dictionary or name (T1) to specific type to populate.</param>
        private void GetSpecificTypeFromArgumentValue(IGenericType gt, object argumentValue, IDictionary<IGenericTypeParameter, IPythonType> specificTypes) {
            switch (argumentValue) {
                case IPythonDictionary dict when gt.GenericParameters.Count == 2:
                    var keyType = dict.Keys.FirstOrDefault()?.GetPythonType();
                    var valueType = dict.Values.FirstOrDefault()?.GetPythonType();
                    if (!keyType.IsUnknown()) {
                        specificTypes[gt.GenericParameters[0]] = keyType;
                    }
                    if (!valueType.IsUnknown()) {
                        specificTypes[gt.GenericParameters[1]] = valueType;
                    }
                    break;
                case IPythonIterable iter when gt.TypeId == BuiltinTypeId.List && gt.GenericParameters.Count == 1:
                    var itemType = iter.GetIterator().Next.GetPythonType();
                    if (!itemType.IsUnknown()) {
                        specificTypes[gt.GenericParameters[0]] = itemType;
                    }
                    break;
                case IPythonCollection coll when gt.TypeId == BuiltinTypeId.Tuple && gt.GenericParameters.Count >= 1:
                    var itemTypes = coll.Contents.Select(m => m.GetPythonType()).ToArray();
                    for (var i = 0; i < Math.Min(itemTypes.Length, gt.GenericParameters.Count); i++) {
                        specificTypes[gt.GenericParameters[i]] = itemTypes[i];
                    }
                    break;
            }
        }

        /// <summary>
        /// Transfers members from generic class to the specific class type
        /// while instantiating specific types for the members.
        /// </summary>
        private void SetClassMembers(PythonClassType classType, IArgumentSet args) {
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
                    case IPythonTemplateType tt when tt.IsGeneric(): {
                            var specificType = tt.CreateSpecificType(args);
                            classType.AddMember(m.Key, specificType, true);
                            break;
                        }
                    case IPythonInstance inst: {
                            var t = inst.GetPythonType();
                            IPythonType specificType = null;
                            switch (t) {
                                case IPythonTemplateType tt when tt.IsGeneric():
                                    specificType = tt.CreateSpecificType(args);
                                    break;
                                case IGenericTypeParameter gtd:
                                    classType.ActualGenericParameters.TryGetValue(gtd, out specificType);
                                    break;
                            }

                            if (specificType != null) {
                                classType.AddMember(m.Key, new PythonInstance(specificType), true);
                            }
                            break;
                        }
                }
            }
        }

        /// <summary>
        /// Determines if the class is generic.
        /// A class is generic if it has at least one unfilled generic type parameters or one of its bases is generic
        /// </summary>
        private void DecideGeneric() {
            using (_genericResolutionGuard.Push(this, out var reentered)) {
                if (!reentered) {
                    IsGeneric = !GenericParameters.IsNullOrEmpty() || (Bases?.OfType<IGenericType>().Any(g => g.IsGeneric) ?? false);
                }
            }
        }

        private void SetNames() {
            // Based on available data, calculate name of generic with parameters, if any,
            // as well as qualified name.
            if (!_genericActualParameters.IsNullOrEmpty()) {
                _nameWithParameters = CodeFormatter.FormatSequence(BaseName, '[', _genericActualParameters.Values);
                _qualifiedNameWithParameters = CodeFormatter.FormatSequence(BaseName, '[', _genericActualParameters.Values.Select(v => v.QualifiedName));
            }
        }

        private void SetGenericParameters() {
            _genericParameters = _genericActualParameters.Values.Distinct().OfType<IGenericTypeParameter>().ToList();
            // Now that parameters are set, check if class is generic
            DecideGeneric();
        }
    }
}
