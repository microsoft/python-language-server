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
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Caching.Factories {
    internal sealed class ModuleFactory : IDisposable {
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
            if (!TypeNames.DeconstructQualifiedName(qualifiedName, out var moduleQualifiedName, out var moduleName, out var typeName, out var isInstance)) {
                return null;
            }

            if(string.IsNullOrEmpty(typeName)) {
                // TODO: resolve from database first?
                return Module.Interpreter.ModuleResolution.GetOrLoadModule(moduleName);
            }

            // Construct complex types from parts, such as Union[typing.Any, a.b.c]
            var typeArgs = Array.Empty<IPythonType>();
            var openBracket = typeName.IndexOf('[');
            if (openBracket > 0) {
                var closeBracket = typeName.LastIndexOf(']');
                if (closeBracket > 0) {
                    var argumentString = typeName.Substring(openBracket + 1, closeBracket - openBracket - 1);
                    var arguments = argumentString.Split(',').Select(s => s.Trim());
                    typeArgs = arguments.Select(ConstructType).ToArray();
                    typeName = typeName.Substring(0, openBracket);
                }
            }

            var member = moduleName == Module.Name
                ? GetMemberFromThisModule(typeName)
                : GetMemberFromModule(moduleQualifiedName, moduleName, typeName, typeArgs);

            return isInstance && member != null ? new PythonInstance(member.GetPythonType()) : member;
        }

        private IMember GetMemberFromModule(string moduleQualifiedName, string moduleName, string typeName, IReadOnlyList<IPythonType> typeArgs) {
            var typeNameParts = typeName.Split('.');

            // TODO: Try resolving from database first.
            var module = Module.Interpreter.ModuleResolution.GetOrLoadModule(moduleName);

            var member = module?.GetMember(typeNameParts[0]);
            foreach (var p in typeNameParts.Skip(1)) {
                var mc = member as IMemberContainer;

                Debug.Assert(mc != null);
                member = mc?.GetMember(p);

                if (member == null) {
                    Debug.Assert(member != null);
                    break;
                }
            }
            return typeArgs.Any() && member is IGenericType gt
                ? gt.CreateSpecificType(typeArgs)
                : member;
        }

        private IMember GetMemberFromThisModule(string typeName) {
            var typeNameParts = typeName.Split('.');
            if (typeNameParts.Length == 0) {
                return null;
            }

            // TODO: nested classes, etc (traverse parts and recurse).
            return ClassFactory.TryCreate(typeNameParts[0])
                        ?? (FunctionFactory.TryCreate(typeNameParts[0])
                            ?? (IMember)VariableFactory.TryCreate(typeNameParts[0]));
        }


    }
}
