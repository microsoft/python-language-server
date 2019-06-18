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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Specializations.Typing.Types;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Utilities;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Caching.Factories {
    internal sealed class ModuleFactory : IDisposable {
        private static readonly ReentrancyGuard<string> _processing = new ReentrancyGuard<string>();

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

        public IMember ConstructMember(string rawQualifiedName) {
            if (!TypeNames.DeconstructQualifiedName(rawQualifiedName, out _, out var nameParts, out var isInstance)) {
                return null;
            }

            // TODO: better resolve circular references.
            if (!_processing.Push(rawQualifiedName) || nameParts.Count < 2) {
                return null;
            }

            try {
                // See if member is a module first.
                var moduleName = nameParts[0];
                var module = moduleName == Module.Name ? Module : Module.Interpreter.ModuleResolution.GetOrLoadModule(moduleName);
                if (module == null) {
                    return null;
                }

                var member = moduleName == Module.Name
                        ? GetMemberFromThisModule(nameParts, 1)
                        : GetMemberFromModule(module, nameParts, 1);

                return isInstance && member != null ? new PythonInstance(member.GetPythonType()) : member;
            } finally {
                _processing.Pop();
            }
        }

        private IMember GetMemberFromModule(IPythonModule module, IReadOnlyList<string> nameParts, int index) {
            if (index >= nameParts.Count) {
                return null;
            }

            var member = module?.GetMember(nameParts[index++]);
            for (; index < nameParts.Count; index++) {
                var memberName = nameParts[index];
                var typeArgs = GetTypeArguments(memberName, out var typeName);

                var mc = member as IMemberContainer;

                Debug.Assert(mc != null);
                member = mc?.GetMember(memberName);

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

        private IMember GetMemberFromThisModule(IReadOnlyList<string> nameParts, int index) {
            if (index >= nameParts.Count) {
                return null;
            }

            // TODO: nested classes, etc (traverse parts and recurse).
            var name = nameParts[index];
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
                    var arguments = argumentString.Split(',').Select(s => s.Trim()).ToArray();
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
