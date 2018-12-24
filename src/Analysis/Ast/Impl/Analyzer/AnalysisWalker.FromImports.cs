﻿// Copyright(c) Microsoft Corporation
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

using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    internal sealed partial class AnalysisWalker {
        public override async Task<bool> WalkAsync(FromImportStatement node, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            if (node.Root == null || node.Names == null) {
                return false;
            }

            var rootNames = node.Root.Names;
            if (rootNames.Count == 1) {
                switch (rootNames[0].Name) {
                    case "__future__":
                        return false;
                        //case "typing":
                        //    ImportMembersFromTyping(node);
                        //    return false;
                }
            }

            var importSearchResult = _interpreter.ModuleResolution.CurrentPathResolver.FindImports(_module.FilePath, node);
            switch (importSearchResult) {
                case ModuleImport moduleImport when moduleImport.FullName == _module.Name:
                    ImportMembersFromSelf(node);
                    return false;
                case ModuleImport moduleImport:
                    await ImportMembersFromModuleAsync(node, moduleImport.FullName, cancellationToken);
                    return false;
                case PossibleModuleImport possibleModuleImport:
                    await ImportMembersFromModuleAsync(node, possibleModuleImport.PossibleModuleFullName, cancellationToken);
                    return false;
                case PackageImport packageImports:
                    await ImportMembersFromPackageAsync(node, packageImports, cancellationToken);
                    return false;
                default:
                    return false;
            }
        }

        private void ImportMembersFromSelf(FromImportStatement node) {
            var names = node.Names;
            var asNames = node.AsNames;

            if (names.Count == 1 && names[0].Name == "*") {
                // from self import * won't define any new members
                return;
            }

            for (var i = 0; i < names.Count; i++) {
                if (asNames[i] == null) {
                    continue;
                }

                var importName = names[i].Name;
                var memberReference = asNames[i];
                var memberName = memberReference.Name;

                var member = _module.GetMember(importName);
                _lookup.DeclareVariable(memberName, member ?? _lookup.UnknownType, GetLoc(names[i]));
            }
        }

        private async Task ImportMembersFromModuleAsync(FromImportStatement node, string moduleName, CancellationToken cancellationToken = default) {
            var names = node.Names;
            var asNames = node.AsNames;
            var module = await _interpreter.ModuleResolution.ImportModuleAsync(moduleName, cancellationToken);

            if (names.Count == 1 && names[0].Name == "*") {
                await HandleModuleImportStarAsync(module, cancellationToken);
                return;
            }

            for (var i = 0; i < names.Count; i++) {
                cancellationToken.ThrowIfCancellationRequested();

                var memberReference = asNames[i] ?? names[i];
                var memberName = memberReference.Name;

                var type = module?.GetMember(memberReference.Name);
                _lookup.DeclareVariable(memberName, type, names[i]);
            }
        }

        private async Task HandleModuleImportStarAsync(IPythonModule module, CancellationToken cancellationToken = default) {
            foreach (var memberName in module.GetMemberNames()) {
                cancellationToken.ThrowIfCancellationRequested();

                var member = module.GetMember(memberName);
                if (member == null) {
                    _log?.Log(TraceEventType.Verbose, $"Undefined import: {module.Name}, {memberName}");
                } else if (member.MemberType == PythonMemberType.Unknown) {
                    _log?.Log(TraceEventType.Verbose, $"Unknown import: {module.Name}, {memberName}");
                }

                member = member ?? _lookup.UnknownType;
                if (member is IPythonModule m) {
                    await _interpreter.ModuleResolution.ImportModuleAsync(m.Name, cancellationToken);
                }

                _lookup.DeclareVariable(memberName, member, module.Location);
            }
        }

        private async Task ImportMembersFromPackageAsync(FromImportStatement node, PackageImport packageImport, CancellationToken cancellationToken = default) {
            var names = node.Names;
            var asNames = node.AsNames;

            if (names.Count == 1 && names[0].Name == "*") {
                // TODO: Need tracking of previous imports to determine possible imports for namespace package. For now import nothing
                _lookup.DeclareVariable("*", _lookup.UnknownType, GetLoc(names[0]));
                return;
            }

            for (var i = 0; i < names.Count; i++) {
                var importName = names[i].Name;
                var memberReference = asNames[i] ?? names[i];
                var memberName = memberReference.Name;
                var location = GetLoc(memberReference);

                ModuleImport moduleImport;
                IPythonType member;
                if ((moduleImport = packageImport.Modules.FirstOrDefault(mi => mi.Name.EqualsOrdinal(importName))) != null) {
                    member = await _interpreter.ModuleResolution.ImportModuleAsync(moduleImport.FullName, cancellationToken);
                } else {
                    member = _lookup.UnknownType;
                }

                _lookup.DeclareVariable(memberName, member, location);
            }
        }
    }
}
