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
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Handlers {
    internal sealed partial class ImportHandler {
        public async Task<bool> HandleFromImportAsync(FromImportStatement node, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            if (node.Root == null || node.Names == null || Module.ModuleType == ModuleType.Specialized) {
                return false;
            }

            var rootNames = node.Root.Names;
            if (rootNames.Count == 1) {
                var rootName = rootNames[0].Name;
                if (rootName.EqualsOrdinal("__future__")) {
                    return false;
                }
            }

            var imports = ModuleResolution.CurrentPathResolver.FindImports(Module.FilePath, node);
            switch (imports) {
                case ModuleImport moduleImport when moduleImport.FullName == Module.Name:
                    await ImportMembersFromSelfAsync(node, cancellationToken);
                    break;
                case ModuleImport moduleImport:
                    await ImportMembersFromModuleAsync(node, moduleImport.FullName, cancellationToken);
                    break;
                case PossibleModuleImport possibleModuleImport:
                    var module = await HandlePossibleImportAsync(possibleModuleImport, possibleModuleImport.PossibleModuleFullName, Eval.GetLoc(node.Root), cancellationToken);
                    if (module != null) {
                        await ImportMembersFromModuleAsync(node, module, cancellationToken);
                    }
                    break;
                case PackageImport packageImports:
                    await ImportMembersFromPackageAsync(node, packageImports, cancellationToken);
                    break;
                case ImportNotFound notFound:
                    MakeUnresolvedImport(null, notFound.FullName, Eval.GetLoc(node.Root));
                    break;
            }
            return false;
        }

        private async Task ImportMembersFromSelfAsync(FromImportStatement node, CancellationToken cancellationToken = default) {
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
                if (member == null && Eval.Module == Module) {
                    // We are still evaluating this module so members are not complete yet.
                    // Consider 'from . import path as path' in os.pyi in typeshed.
                    var import = ModuleResolution.CurrentPathResolver.GetModuleImportFromModuleName($"{Module.Name}.{importName}");
                    if (!string.IsNullOrEmpty(import?.FullName)) {
                        member = await ModuleResolution.ImportModuleAsync(import.FullName, cancellationToken);
                    }
                }
                Eval.DeclareVariable(memberName, member ?? Eval.UnknownType, VariableSource.Declaration, Eval.GetLoc(names[i]));
            }
        }

        private async Task ImportMembersFromModuleAsync(FromImportStatement node, string moduleName, CancellationToken cancellationToken = default) {
            var module = await ModuleResolution.ImportModuleAsync(moduleName, cancellationToken);
            if (module != null) {
                await ImportMembersFromModuleAsync(node, module, cancellationToken);
            }
        }

        private async Task ImportMembersFromModuleAsync(FromImportStatement node, IPythonModule module, CancellationToken cancellationToken = default) {
            var names = node.Names;
            var asNames = node.AsNames;
            if (names.Count == 1 && names[0].Name == "*") {
                // TODO: warn this is not a good style per
                // TODO: https://docs.python.org/3/faq/programming.html#what-are-the-best-practices-for-using-import-in-a-module
                // TODO: warn this is invalid if not in the global scope.
                await HandleModuleImportStarAsync(module, cancellationToken);
                return;
            }

            Eval.DeclareVariable(module.Name, module, VariableSource.Import, node);

            for (var i = 0; i < names.Count; i++) {
                cancellationToken.ThrowIfCancellationRequested();

                var memberName = names[i].Name;
                if (!string.IsNullOrEmpty(memberName)) {
                    var variableName = asNames[i]?.Name ?? memberName;
                    var type = module.GetMember(memberName) ?? Interpreter.UnknownType;
                    Eval.DeclareVariable(variableName, type, VariableSource.Import, names[i]);
                }
            }
        }

        private async Task HandleModuleImportStarAsync(IPythonModule module, CancellationToken cancellationToken = default) {
            foreach (var memberName in module.GetMemberNames()) {
                cancellationToken.ThrowIfCancellationRequested();

                var member = module.GetMember(memberName);
                if (member == null) {
                    Log?.Log(TraceEventType.Verbose, $"Undefined import: {module.Name}, {memberName}");
                } else if (member.MemberType == PythonMemberType.Unknown) {
                    Log?.Log(TraceEventType.Verbose, $"Unknown import: {module.Name}, {memberName}");
                }

                member = member ?? Eval.UnknownType;
                if (member is IPythonModule m) {
                    await ModuleResolution.ImportModuleAsync(m.Name, cancellationToken);
                }

                Eval.DeclareVariable(memberName, member, VariableSource.Import, module.Location);
            }
        }

        private async Task ImportMembersFromPackageAsync(FromImportStatement node, PackageImport packageImport, CancellationToken cancellationToken = default) {
            var names = node.Names;
            var asNames = node.AsNames;

            if (names.Count == 1 && names[0].Name == "*") {
                // TODO: Need tracking of previous imports to determine possible imports for namespace package. For now import nothing
                Eval.DeclareVariable("*", Eval.UnknownType, VariableSource.Import, Eval.GetLoc(names[0]));
                return;
            }

            for (var i = 0; i < names.Count; i++) {
                var importName = names[i].Name;
                var memberReference = asNames[i] ?? names[i];
                var memberName = memberReference.Name;
                var location = Eval.GetLoc(memberReference);

                ModuleImport moduleImport;
                IPythonType member;
                if ((moduleImport = packageImport.Modules.FirstOrDefault(mi => mi.Name.EqualsOrdinal(importName))) != null) {
                    member = await ModuleResolution.ImportModuleAsync(moduleImport.FullName, cancellationToken);
                } else {
                    member = Eval.UnknownType;
                }

                Eval.DeclareVariable(memberName, member, VariableSource.Import, location);
            }
        }
    }
}
