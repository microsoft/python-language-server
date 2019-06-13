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
            if (!SplitQualifiedName(qualifiedName, out var moduleName, out var typeNameParts, out var isInstance)) {
                return null;
            }

            Debug.Assert(typeNameParts.Count > 0);
            var member = moduleName == Module.Name
                ? GetMemberFromThisModule(typeNameParts)
                : GetMemberFromModule(moduleName, typeNameParts);

            return isInstance && member != null ? new PythonInstance(member.GetPythonType()) : member;
        }

        private IMember GetMemberFromModule(string moduleName, IReadOnlyList<string> typeNameParts) {
            // Module resolution will call back to the module database
            // to get persisted analysis, if available.
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
            return member;
        }

        private IMember GetMemberFromThisModule(IReadOnlyList<string> typeNameParts) {
            if (typeNameParts.Count == 0) {
                return null;
            }

            // TODO: nested classes, etc (traverse parts and recurse).
            return ClassFactory.TryCreate(typeNameParts[0])
                        ?? (FunctionFactory.TryCreate(typeNameParts[0])
                            ?? (IMember)VariableFactory.TryCreate(typeNameParts[0]));
        }

        private bool SplitQualifiedName(string qualifiedName, out string moduleName, out List<string> typeNameParts, out bool isInstance) {
            moduleName = null;
            typeNameParts = new List<string>();
            isInstance = false;

            if (string.IsNullOrEmpty(qualifiedName)) {
                return false;
            }

            if (qualifiedName == "..." || qualifiedName == "ellipsis") {
                moduleName = @"builtins";
                typeNameParts.Add("...");
                return true;
            }

            isInstance = qualifiedName.StartsWith("i:");
            qualifiedName = isInstance ? qualifiedName.Substring(2) : qualifiedName;
            var components = qualifiedName.Split('.');
            switch (components.Length) {
                case 0:
                    return false;
                case 1:
                    moduleName = @"builtins";
                    typeNameParts.Add(components[0]);
                    return true;
                default:
                    moduleName = components[0];
                    typeNameParts.AddRange(components.Skip(1));
                    return true;
            }
        }

    }
}
