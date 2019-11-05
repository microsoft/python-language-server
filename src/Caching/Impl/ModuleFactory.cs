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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Python.Analysis.Caching.Lazy;
using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Specializations.Typing.Types;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Utilities;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Caching {
    /// <summary>
    /// Constructs module from its persistent model.
    /// </summary>
    internal sealed class ModuleFactory {
        /// <summary>For use in tests so missing members will assert.</summary>
        internal static bool EnableMissingMemberAssertions { get; set; }

        private static readonly ConcurrentDictionary<string, PythonDbModule> _modulesCache
            = new ConcurrentDictionary<string, PythonDbModule>();

        // TODO: better resolve circular references.
        private readonly ReentrancyGuard<string> _moduleReentrancy = new ReentrancyGuard<string>();
        private readonly ModuleModel _model;
        private readonly IGlobalScope _gs;
        private readonly ModuleDatabase _db;
        private readonly IServiceContainer _services;

        public IPythonModule Module { get; }
        public Location DefaultLocation { get; }

        public ModuleFactory(ModuleModel model, IPythonModule module, IGlobalScope gs, IServiceContainer services) {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _gs = gs ?? throw new ArgumentNullException(nameof(gs));
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _db = services.GetService<ModuleDatabase>();
            Module = module ?? throw new ArgumentNullException(nameof(module));
            DefaultLocation = new Location(Module);
        }

        public IPythonType ConstructType(string qualifiedName)
            => ConstructMember(qualifiedName)?.GetPythonType() ?? Module.Interpreter.UnknownType;

        public IMember ConstructMember(string qualifiedName) {
            // Determine module name, member chain and if this is an instance.
            if (!TypeNames.DeconstructQualifiedName(qualifiedName, out var parts)) {
                return null;
            }

            // See if member is a module first.
            var module = GetModule(parts);
            if (module == null) {
                return null;
            }

            var member = parts.ModuleName == Module.Name
                    ? GetMemberFromThisModule(parts.MemberNames)
                    : GetMemberFromModule(module, parts.MemberNames);

            if (parts.ObjectType != ObjectType.Instance) {
                return member;
            }

            var t = member.GetPythonType() ?? module.Interpreter.UnknownType;
            return new PythonInstance(t);
        }

        private IMember GetMemberFromThisModule(IReadOnlyList<string> memberNames) {
            if (memberNames.Count == 0) {
                return null;
            }

            // Try from cache first
            MemberModel currentModel = _model;
            IMember m = null;
            IPythonType declaringType = null;

            foreach (var name in memberNames) {
                // Check if name has type arguments such as Union[int, str]
                // Note that types can be nested like Union[int, Union[A, B]]
                var memberName = name;
                var typeArgs = GetTypeArguments(memberName, out var typeName);
                if (!string.IsNullOrEmpty(typeName) && typeName != name) {
                    memberName = typeName;
                }

                if (memberName == "<lambda>") {
                    return new PythonFunctionType("<lambda>", default, default, string.Empty);
                }

                var nextModel = currentModel.GetModel(memberName);
                Debug.Assert(nextModel != null,
                    $"Unable to find {string.Join(".", memberNames)} in module {Module.Name}");
                if (nextModel == null) {
                    return null;
                }

                m = MemberFactory.CreateMember(nextModel, this, _gs, declaringType);
                Debug.Assert(m != null);

                if (m is IGenericType gt && typeArgs.Count > 0) {
                    m = gt.CreateSpecificType(new ArgumentSet(typeArgs, null, null));
                }

                currentModel = nextModel;
                declaringType = m.GetPythonType();
                Debug.Assert(declaringType != null);
            }

            return m;
        }

        private IMember GetMemberFromModule(IPythonModule module, IReadOnlyList<string> memberNames)
            => memberNames.Count == 0 ? module : GetMember(module, memberNames);


        private IPythonModule GetModule(QualifiedNameParts parts) {
            if (parts.ModuleName == Module.Name) {
                return Module;
            }

            using (_moduleReentrancy.Push(parts.ModuleName, out var reentered)) {
                if (reentered) {
                    return null;
                }

                // If module is loaded, then use it. Otherwise, create DB module but don't restore it just yet.
                var module = Module.Interpreter.ModuleResolution.GetImportedModule(parts.ModuleName);
                if (module == null && parts.ModuleId != null && _db != null) {
                    if (!_modulesCache.TryGetValue(parts.ModuleId, out var m)) {
                        if (_db.FindModuleModelById(parts.ModuleName, parts.ModuleId, ModuleType.Specialized, out var model)) {
                            // DeclareMember db module, but do not reconstruct the analysis just yet.
                            _modulesCache[parts.ModuleId] = m = new PythonDbModule(model, model.FilePath, _services);
                            module = m;
                        }
                    }
                }

                // Here we do not call GetOrLoad since modules references here must
                // either be loaded already since they were required to create
                // persistent state from analysis. Also, occasionally types come
                // from the stub and the main module was never loaded. This, for example,
                // happens with io which has member with mmap type coming from mmap
                // stub rather than the primary mmap module.
                if (module != null) {
                    return parts.ObjectType == ObjectType.VariableModule ? new PythonVariableModule(module) : module;
                }
                return null;
            }
        }

        private IMember GetMember(IMember root, IEnumerable<string> memberNames) {
            var member = root;
            foreach (var n in memberNames) {
                var memberName = n;
                // Check if name has type arguments such as Union[int, str]
                // Note that types can be nested like Union[int, Union[A, B]]
                var typeArgs = GetTypeArguments(memberName, out var typeName);
                if (!string.IsNullOrEmpty(typeName) && typeName != memberName) {
                    memberName = typeName;
                }

                var mc = member as IMemberContainer;
                Debug.Assert(mc != null);

                if (mc is IBuiltinsPythonModule builtins) {
                    // Builtins require special handling since there may be 'hidden' names
                    // which need to be mapped to visible types.
                    member = GetBuiltinMember(builtins, memberName, typeArgs) ?? builtins.Interpreter.UnknownType;
                } else {
                    member = mc?.GetMember(memberName);
                    // Work around problem that some stubs have incorrectly named tuples.
                    // For example, in posix.pyi variable for the named tuple is not named as the tuple:
                    // sched_param = NamedTuple('sched_priority', [('sched_priority', int),])
                    member = member ?? (mc as PythonModule)?.GlobalScope.Variables
                             .FirstOrDefault(v => v.Value is ITypingNamedTupleType nt && nt.Name == memberName);
                }

                if (member == null) {
                    var containerName = mc is IPythonType t ? t.Name : "<mc>";
                    Debug.Assert(member != null || EnableMissingMemberAssertions == false, $"Unable to find member {memberName} in {containerName}.");
                    break;
                }

                member = typeArgs.Count > 0 && member is IGenericType gt
                    ? gt.CreateSpecificType(new ArgumentSet(typeArgs, null, null))
                    : member;
            }

            return member;
        }

        private IMember GetBuiltinMember(IBuiltinsPythonModule builtins, string memberName, IReadOnlyList<IPythonType> typeArgs) {
            if (memberName.StartsWithOrdinal("__")) {
                memberName = memberName.Substring(2, memberName.Length - 4);
            }

            switch (memberName) {
                case "None":
                    return builtins.Interpreter.GetBuiltinType(BuiltinTypeId.None);
                case "Unknown":
                    return builtins.Interpreter.UnknownType;
                case "SuperType":
                    return new PythonSuperType(typeArgs, builtins);
            }
            return builtins.GetMember(memberName);
        }

        private IReadOnlyList<IPythonType> GetTypeArguments(string memberName, out string typeName) {
            typeName = null;
            // TODO: better handle generics.
            // https://github.com/microsoft/python-language-server/issues/1215
            // Determine generic type arguments, if any, so we can construct
            // complex types from parts, such as Union[typing.Any, a.b.c].
            var typeArgs = new List<IPythonType>();
            var openBracket = memberName.IndexOf('[');
            if (openBracket > 0) {
                var closeBracket = memberName.LastIndexOf(']');
                if (closeBracket > 0) {
                    var argumentString = memberName.Substring(openBracket + 1, closeBracket - openBracket - 1);
                    // Extract type names from argument string. Note that types themselves
                    // can have arguments: Union[int, Union[int, Union[str, bool]], ...].
                    var qualifiedNames = TypeNames.GetTypeNames(argumentString, ',');
                    foreach (var qn in qualifiedNames) {
                        var t = ConstructType(qn);
                        if (t == null) {
                            TypeNames.DeconstructQualifiedName(qn, out var parts);
                            typeName = string.Join(".", parts.MemberNames);
                            t = new GenericTypeParameter(typeName, Module, Array.Empty<IPythonType>(), null, null, null, default);
                        }
                        typeArgs.Add(t);
                    }
                    typeName = memberName.Substring(0, openBracket);
                }
            }
            return typeArgs;
        }
    }
}
