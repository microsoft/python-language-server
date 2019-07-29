using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Specializations.Typing.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Types {
    internal partial class PythonClassType : PythonType, IPythonClassType, IPythonTemplateType, IEquatable<IPythonClassType> {

        private IPythonType[] GetTypeParameters() {
            var fromBases = new HashSet<IPythonType>();
            foreach (var b in Bases) {
                switch (b) {
                    case IGenericClassParameter g:
                        if (g.TypeDefinitions != null) {
                            fromBases.UnionWith(g.TypeDefinitions);
                        }
                        break;
                    case IGenericType t:
                        if (t.Parameters != null) {
                            fromBases.UnionWith(t.Parameters);
                        }
                        break;
                    case IPythonClassType p:
                        fromBases.UnionWith(p.GetUnfilledTypeParameters());
                        break;
                }
            }

            var fromSelf = GenericParameters.Values.Where(v => v is IGenericTypeDefinition);
            return fromBases.Concat(fromSelf).ToArray();
        }

        public IPythonType CreateSpecificType(IArgumentSet args) {
            // Create map of names listed in Generic[...] in the class definition.
            // We will be filling the map with specific types, if any provided.
            var genericTypeParameters = GetTypeParameters();

            // maps generic type parameter to the specific type for it
            var genericTypeToSpecificType = genericTypeParameters.ToDictionary(gtp => gtp.Name, gtp => gtp);

            // new specific python class types
            var newBases = new List<IPythonType>();

            var validArgs = args.Arguments;

            // Arguments passed are those of __init__ or it is a copy constructor.
            // They do not necessarily match all of the declared generic parameters.
            // Some generic parameters may be used to specify method return types or
            // method arguments and do not appear in the constructor argument list.
            // Figure out whatever specific types we can from the arguments.
            foreach (var arg in validArgs) {
                // The argument may either match generic type definition of be a specific type
                // created off generic type. Consider '__init__(self, v: _T)' and
                // 'class A(Generic[K, V], Mapping[K, V])'.

                // don't look at any values that are type variables

                // if argument is annotated with a IGenericTypeDefinition (this comes from constructor or setter)
                if (arg.Type is IGenericTypeDefinition argTypeDefinition) {
                    // Parameter is annotated as generic type definition. Check if its generic type
                    // name matches any of the generic class parameters. I.e. if there is
                    // an argument like 'v: _T' we need to check if class has matching Generic[_T].

                    // check if we are looking for the name of the annotated variable
                    if (genericTypeToSpecificType.ContainsKey(argTypeDefinition.Name)) {
                        // TODO: Check if specific type matches generic type definition constraints and report mismatches.
                        // Assign specific type.

                        if (arg.Value is IMember m && m.GetPythonType() is IPythonType pt) {
                            genericTypeToSpecificType[argTypeDefinition.Name] = pt;
                        } else {
                            // TODO: report supplied parameter is not a type.
                        }
                    } else {
                        // TODO: report generic parameter name mismatch.
                    }
                    continue;
                }

                if (!(arg.Value is IGenericTypeDefinition) && arg.Value is IMember member && !member.GetPythonType().IsUnknown()) {
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
            for (int i = 0, gtIndex = 0; i < validArgs.Count; i++) {
                var arg = validArgs[i];

                if (Equals(arg.Type)) {
                    continue; // Typically 'self'.
                }

                if (arg.Value is IMember member) {
                    var type = member.GetPythonType();
                    if (!type.IsUnknown()) {
                        var gtd = gtIndex < genericTypeParameters.Length ? genericTypeParameters[gtIndex] : null;
                        if (gtd != null && genericTypeToSpecificType.TryGetValue(gtd.Name, out var s) && s is IGenericTypeDefinition) {
                            genericTypeToSpecificType[gtd.Name] = type;
                        }
                        gtIndex++;
                    }
                }
            }

            PythonClassType classType = new PythonClassType(_name, new Location(DeclaringModule));

            // Methods returning generic types need to know how to match generic
            // parameter name to the actual supplied type.
            StoreGenericParameters(classType, genericTypeParameters, genericTypeToSpecificType);

            // Prevent reentrancy when resolving generic class where
            // method may be returning instance of type of the same class.
            if (!Push(classType)) {
                return _processing;
            }

            try {
                // Get declared generic parameters of the class, i.e. list of Ts in Generic[T1, T2, ...]
                var genericClassParameters = Bases.OfType<IGenericClassParameter>().ToArray();

                // Create specific bases since we may have generic types there.
                // Match generic parameter names to base type parameter names.
                // Consider 'class A(Generic[T], List[T], Mapping[E, F]): ...'
                var genericTypeBases = Bases.Except(genericClassParameters).OfType<IGenericType>().ToArray();

                // Removing all generic bases, and will gradually add them back after making them specific
                var bases = Bases.Except(genericTypeBases).Except(genericClassParameters).ToList();

                // Create specific types for generic type bases
                // it for generic types but not Generic[T, ...] itself.
                foreach (var gt in genericTypeBases) {
                    var st = gt.Parameters
                        .Select(p => classType.GenericParameters.TryGetValue(p.Name, out var t) ? t : null)
                        .Where(p => !p.IsUnknown())
                        .ToArray();
                    if (st.Length > 0) {
                        var type = gt.CreateSpecificType(new ArgumentSet(st, args.Expression, args.Eval));
                        if (!type.IsUnknown()) {
                            bases.Add(type);
                        }
                    }
                }

                var ret = bases.Concat(newBases).ToList();

                // reinstantiate the bases so that they have the new type information
                // Set specific class bases
                classType.SetBases(ret);
            } finally {
                Pop();
            }

            // Transfer members from generic to specific type.
            SetClassMembers(classType, args);
            return classType;
        }

        private void StoreGenericParameters(PythonClassType classType, IPythonType[] genericParameters, Dictionary<string, IPythonType> genericToSpecificTypes) {
            classType._genericParameters = new Dictionary<string, IPythonType>();
            for (var i = 0; i < genericParameters.Length; i++) {
                var gb = genericParameters[i];
                classType._genericParameters[gb.Name] = genericToSpecificTypes.TryGetValue(gb.Name, out var v) ? v : null;
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
                                case IGenericTypeDefinition gtd:
                                    classType.GenericParameters.TryGetValue(gtd.Name, out specificType);
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
    }
}
