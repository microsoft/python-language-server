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
using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Specializations.Typing.Types;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Utilities;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Caching.Factories {
    internal sealed class ModuleFactory : IDisposable {
        private readonly ReentrancyGuard<string> _processing = new ReentrancyGuard<string>();

        public IPythonModule Module { get; }
        public ClassFactory ClassFactory { get; }
        public FunctionFactory FunctionFactory { get; }
        public PropertyFactory PropertyFactory { get; }
        public VariableFactory VariableFactory { get; }
        public Location DefaultLocation { get; }

        public ModuleFactory(ModuleModel model, IPythonModule module) {
            Module = module;
            ClassFactory = new ClassFactory(model.Classes, this);
            FunctionFactory = new FunctionFactory(model.Functions, this);
            VariableFactory = new VariableFactory(model.Variables, this);
            PropertyFactory = new PropertyFactory(this);
            DefaultLocation = new Location(Module);
        }

        public void Dispose() {
            ClassFactory.Dispose();
            FunctionFactory.Dispose();
            VariableFactory.Dispose();
        }

        public IPythonType ConstructType(string qualifiedName) => ConstructMember(qualifiedName)?.GetPythonType();

        public IMember ConstructMember(string qualifiedName) {
            // Determine module name, member chain and if this is an instance.
            if (!TypeNames.DeconstructQualifiedName(qualifiedName, out var parts)) {
                return null;
            }

            // TODO: better resolve circular references.
            if (!_processing.Push(qualifiedName)) {
                return null;
            }

            try {
                // See if member is a module first.
                IPythonModule module = null;
                if (parts.ModuleName == Module.Name) {
                    module = Module;
                } else {
                    // Here we do not call GetOrLoad since modules references here must
                    // either be loaded already since they were required to create
                    // persistent state from analysis. Also, occasionally types come
                    // from the stub and the main module was never loaded. This, for example,
                    // happens with io which has member with mmap type coming from mmap
                    // stub rather than the primary mmap module.
                    var m = Module.Interpreter.ModuleResolution.GetImportedModule(parts.ModuleName);
                    // Try stub-only case (ex _importlib_modulespec).
                    m = m ?? Module.Interpreter.TypeshedResolution.GetImportedModule(parts.ModuleName);
                    if (m != null) {
                        module = parts.ObjectType == ObjectType.VariableModule ? new PythonVariableModule(m) : m;
                    }
                }

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
            } finally {
                _processing.Pop();
            }
        }

        private IMember GetMemberFromModule(IPythonModule module, IReadOnlyList<string> memberNames) {
            if (memberNames.Count == 0) {
                return module;
            }

            IMember member = module;
            foreach (var n in memberNames) {
                var memberName = n;
                // Check if name has type arguments such as Union[int, str]
                // Note that types can be nested like Union[int, Union[A, B]]
                var typeArgs = GetTypeArguments(memberName, out var typeName);
                if (typeArgs.Count > 0) {
                    memberName = typeName;
                }

                var mc = member as IMemberContainer;
                Debug.Assert(mc != null);

                if (mc is IBuiltinsPythonModule builtins) {
                    // Builtins require special handling since there may be 'hidden' names
                    // like __NoneType__ which need to be mapped to visible types.
                    member = GetBuiltinMember(builtins, memberName) ?? builtins.Interpreter.UnknownType;
                } else {
                    member = mc?.GetMember(memberName);
                }

                if (member == null) {
                    Debug.Assert(member != null);
                    break;
                }

                member = typeArgs.Any() && member is IGenericType gt
                    ? gt.CreateSpecificType(typeArgs)
                    : member;
            }

            return member;
        }

        private IMember GetBuiltinMember(IBuiltinsPythonModule builtins, string memberName) {
            if (memberName.StartsWithOrdinal("__")) {
                memberName = memberName.Substring(2, memberName.Length - 4);
            }

            switch (memberName) {
                case "NoneType":
                    return builtins.Interpreter.GetBuiltinType(BuiltinTypeId.NoneType);
                case "Unknown":
                    return builtins.Interpreter.UnknownType;
            }
            return builtins.GetMember(memberName);
        }

        private IMember GetMemberFromThisModule(IReadOnlyList<string> memberNames) {
            if (memberNames.Count == 0) {
                return null;
            }

            // TODO: nested classes, etc (traverse parts and recurse).
            var name = memberNames[0];
            return ClassFactory.TryCreate(name)
                        ?? (FunctionFactory.TryCreate(name)
                            ?? (IMember)VariableFactory.TryCreate(name));
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
                    var arguments = TypeNames.GetTypeNames(argumentString, ',');
                    foreach (var a in arguments) {
                        var t = ConstructType(a);
                        // TODO: better handle generics type definitions from TypeVar.
                        // https://github.com/microsoft/python-language-server/issues/1214
                        t = t ?? new GenericTypeParameter(a, Module, Array.Empty<IPythonType>(), string.Empty, DefaultLocation.IndexSpan);
                        typeArgs.Add(t);
                    }
                    typeName = memberName.Substring(0, openBracket);
                }
            }
            return typeArgs;
        }
    }
}
