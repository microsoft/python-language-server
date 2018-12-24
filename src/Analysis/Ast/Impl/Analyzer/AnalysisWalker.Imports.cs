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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    internal sealed partial class AnalysisWalker {
        public override async Task<bool> WalkAsync(ImportStatement node, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            if (node.Names == null) {
                return false;
            }

            var len = Math.Min(node.Names.Count, node.AsNames.Count);
            for (var i = 0; i < len; i++) {
                cancellationToken.ThrowIfCancellationRequested();

                var moduleImportExpression = node.Names[i];
                var importNames = moduleImportExpression.Names.Select(n => n.Name).ToArray();
                var asNameExpression = node.AsNames[i];
                var memberName = asNameExpression?.Name ?? moduleImportExpression.Names[0].Name;

                var imports = _interpreter.ModuleResolution.CurrentPathResolver.GetImportsFromAbsoluteName(_module.FilePath, importNames, node.ForceAbsolute);
                var location = GetLoc(moduleImportExpression);
                IPythonModule module = null;
                switch (imports) {
                    case ModuleImport moduleImport when moduleImport.FullName == _module.Name:
                        _lookup.DeclareVariable(memberName, _module, location);
                        break;
                    case ModuleImport moduleImport:
                        module = await HandleImportAsync(node, moduleImport, cancellationToken);
                        break;
                    case PossibleModuleImport possibleModuleImport:
                        module = await HandlePossibleImportAsync(node, possibleModuleImport, cancellationToken);
                        break;
                    default:
                        // TODO: Package import?
                        MakeUnresolvedImport(memberName, moduleImportExpression);
                        break;
                }

                if (module != null) {
                    AssignImportedVariables(module, moduleImportExpression, asNameExpression);
                }
            }
            return false;
        }

        private async Task<IPythonModule> HandleImportAsync(ImportStatement node, ModuleImport moduleImport, CancellationToken cancellationToken) {
            var module = await _interpreter.ModuleResolution.ImportModuleAsync(moduleImport.FullName, cancellationToken);
            if (module == null) {
                MakeUnresolvedImport(moduleImport.FullName, node);
                return null;
            }
            return module;
        }

        private async Task<IPythonModule> HandlePossibleImportAsync(ImportStatement node, PossibleModuleImport possibleModuleImport, CancellationToken cancellationToken) {
            var fullName = possibleModuleImport.PrecedingModuleFullName;
            var module = await _interpreter.ModuleResolution.ImportModuleAsync(possibleModuleImport.PossibleModuleFullName, cancellationToken);
            if (module == null) {
                MakeUnresolvedImport(possibleModuleImport.PossibleModuleFullName, node);
                return null;
            }

            var nameParts = possibleModuleImport.RemainingNameParts;
            for (var i = 0; i < nameParts.Count; i++) {
                var namePart = nameParts[i];
                var childModule = module.GetMember<IPythonModule>(namePart);
                if (childModule == null) {
                    var unresolvedModuleName = string.Join(".", nameParts.Take(i + 1).Prepend(fullName));
                    MakeUnresolvedImport(unresolvedModuleName, node);
                    return null;
                }
                module = childModule;
            }

            return module;
        }

        private void AssignImportedVariables(IPythonModule module, DottedName moduleImportExpression, NameExpression asNameExpression) {
            // "import fob.oar as baz" is handled as
            // baz = import_module('fob.oar')
            if (asNameExpression != null) {
                _lookup.DeclareVariable(asNameExpression.Name, module, asNameExpression);
                return;
            }

            // "import fob.oar" is handled as
            // import_module('fob.oar')
            // fob = import_module('fob')
            var importNames = moduleImportExpression.Names;

            PythonPackage pythonPackage = null;
            var existingDepth = 0;

            var childPackage = _lookup.GetInScope<PythonPackage>(importNames[0].Name);
            while (childPackage != null && existingDepth < importNames.Count - 1) {
                existingDepth++;
                pythonPackage = childPackage;
                childPackage = pythonPackage.GetMember<PythonPackage>(importNames[existingDepth].Name);
            }

            var child = module;
            for (var i = importNames.Count - 2; i >= existingDepth; i--) {
                var childName = importNames[i + 1].Name;
                var parentName = importNames[i].Name;
                var parent = new PythonPackage(parentName, _services);
                parent.AddChildModule(childName, child);
                child = parent;
            }

            if (pythonPackage == null) {
                _lookup.DeclareVariable(importNames[0].Name, child, importNames[0]);
            } else {
                pythonPackage.AddChildModule(importNames[existingDepth].Name, child);
            }
        }

        private void MakeUnresolvedImport(string name, Node node) => _lookup.DeclareVariable(name, new SentinelModule(name), GetLoc(node));
    }
}
