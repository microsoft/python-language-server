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

using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    internal partial class AnalysisWalker {
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

            var importSearchResult = Interpreter.ModuleResolution.CurrentPathResolver.FindImports(Module.FilePath, node);
            switch (importSearchResult) {
                case ModuleImport moduleImport when moduleImport.FullName == Module.Name:
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

                var member = Module.GetMember(importName);
                Lookup.DeclareVariable(memberName, member ?? Lookup.UnknownType, GetLoc(names[i]));
            }
        }

        private async Task ImportMembersFromModuleAsync(FromImportStatement node, string moduleName, CancellationToken cancellationToken = default) {
            var names = node.Names;
            var asNames = node.AsNames;
            var module = await Interpreter.ModuleResolution.ImportModuleAsync(moduleName, cancellationToken);

            if (names.Count == 1 && names[0].Name == "*") {
                // TODO: warn this is not a good style per
                // TODO: https://docs.python.org/3/faq/programming.html#what-are-the-best-practices-for-using-import-in-a-module
                // TODO: warn this is invalid if not in the global scope.
                await HandleModuleImportStarAsync(module, cancellationToken);
                return;
            }

            for (var i = 0; i < names.Count; i++) {
                cancellationToken.ThrowIfCancellationRequested();

                var memberReference = asNames[i] ?? names[i];
                var memberName = memberReference.Name;

                var type = module?.GetMember(memberReference.Name);
                Lookup.DeclareVariable(memberName, type, names[i]);
            }
        }

        private async Task HandleModuleImportStarAsync(IPythonModuleType module, CancellationToken cancellationToken = default) {
            foreach (var memberName in module.GetMemberNames()) {
                cancellationToken.ThrowIfCancellationRequested();

                var member = module.GetMember(memberName);
                if (member == null) {
                    Log?.Log(TraceEventType.Verbose, $"Undefined import: {module.Name}, {memberName}");
                } else if (member.MemberType == PythonMemberType.Unknown) {
                    Log?.Log(TraceEventType.Verbose, $"Unknown import: {module.Name}, {memberName}");
                }

                member = member ?? Lookup.UnknownType;
                if (member is IPythonModuleType m) {
                    await Interpreter.ModuleResolution.ImportModuleAsync(m.Name, cancellationToken);
                }

                Lookup.DeclareVariable(memberName, member, module.Location);
            }
        }

        private async Task ImportMembersFromPackageAsync(FromImportStatement node, PackageImport packageImport, CancellationToken cancellationToken = default) {
            var names = node.Names;
            var asNames = node.AsNames;

            if (names.Count == 1 && names[0].Name == "*") {
                // TODO: Need tracking of previous imports to determine possible imports for namespace package. For now import nothing
                Lookup.DeclareVariable("*", Lookup.UnknownType, GetLoc(names[0]));
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
                    member = await Interpreter.ModuleResolution.ImportModuleAsync(moduleImport.FullName, cancellationToken);
                } else {
                    member = Lookup.UnknownType;
                }

                Lookup.DeclareVariable(memberName, member, location);
            }
        }
    }
}
