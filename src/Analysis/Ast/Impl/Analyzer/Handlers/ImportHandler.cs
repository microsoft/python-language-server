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
using System.Linq;
using System.Threading;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;
using ErrorCodes = Microsoft.Python.Analysis.Diagnostics.ErrorCodes;

namespace Microsoft.Python.Analysis.Analyzer.Handlers {
    internal sealed partial class ImportHandler : StatementHandler {
        public ImportHandler(AnalysisWalker walker) : base(walker) { }

        public bool HandleImport(ImportStatement node, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            if (Module.ModuleType == ModuleType.Specialized) {
                return false;
            }

            var len = Math.Min(node.Names.Count, node.AsNames.Count);
            for (var i = 0; i < len; i++) {
                cancellationToken.ThrowIfCancellationRequested();

                var moduleImportExpression = node.Names[i];
                var importNames = moduleImportExpression.Names.Select(n => n.Name).ToArray();
                var asNameExpression = node.AsNames[i];
                var memberName = asNameExpression?.Name ?? moduleImportExpression.Names[0].Name;

                var imports = ModuleResolution.CurrentPathResolver.GetImportsFromAbsoluteName(Module.FilePath, importNames, node.ForceAbsolute);
                if (Module.ModuleType == ModuleType.Stub && imports is ModuleImport mi && mi.Name == Module.Name) {
                    continue;
                }

                var location = Eval.GetLoc(moduleImportExpression);
                IPythonModule module = null;
                switch (imports) {
                    case ModuleImport moduleImport when moduleImport.FullName == Module.Name:
                        Eval.DeclareVariable(memberName, Module, VariableSource.Declaration, location);
                        break;
                    case ModuleImport moduleImport:
                        module = HandleImport(moduleImport, location);
                        break;
                    case PossibleModuleImport possibleModuleImport:
                        module = HandlePossibleImport(possibleModuleImport, possibleModuleImport.PossibleModuleFullName, location);
                        break;
                    default:
                        // TODO: Package import?
                        MakeUnresolvedImport(memberName, moduleImportExpression.MakeString(), Eval.GetLoc(moduleImportExpression));
                        break;
                }

                if (module != null) {
                    AssignImportedVariables(module, moduleImportExpression, asNameExpression);
                }
            }

            return false;
        }

        private IPythonModule HandleImport(ModuleImport moduleImport, LocationInfo location) {
            var module = ModuleResolution.GetOrLoadModule(moduleImport.FullName);
            if (module != null) {
                return module;
            }

            MakeUnresolvedImport(moduleImport.FullName, moduleImport.FullName, location);
            return null;
        }

        private IPythonModule HandlePossibleImport(PossibleModuleImport possibleModuleImport, string moduleName, LocationInfo location) {
            var fullName = possibleModuleImport.PrecedingModuleFullName;
            var module = ModuleResolution.GetOrLoadModule(possibleModuleImport.PrecedingModuleFullName);
            if (module == null) {
                MakeUnresolvedImport(possibleModuleImport.PrecedingModuleFullName, moduleName, location);
                return null;
            }

            var nameParts = possibleModuleImport.RemainingNameParts;
            for (var i = 0; i < nameParts.Count; i++) {
                var namePart = nameParts[i];
                var childModule = module.GetMember<IPythonModule>(namePart);
                if (childModule == null) {
                    var unresolvedModuleName = string.Join(".", nameParts.Take(i + 1).Prepend(fullName));
                    MakeUnresolvedImport(unresolvedModuleName, moduleName, location);
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
                Eval.DeclareVariable(asNameExpression.Name, module, VariableSource.Import, asNameExpression);
                return;
            }

            // "import fob.oar" is handled as
            // import_module('fob.oar')
            // fob = import_module('fob')
            var importNames = moduleImportExpression.Names;

            PythonPackage pythonPackage = null;
            var existingDepth = 0;

            var childPackage = Eval.GetInScope<PythonPackage>(importNames[0].Name);
            while (childPackage != null && existingDepth < importNames.Count - 1) {
                existingDepth++;
                pythonPackage = childPackage;
                childPackage = pythonPackage.GetMember<PythonPackage>(importNames[existingDepth].Name);
            }

            var child = module;
            for (var i = importNames.Count - 2; i >= existingDepth; i--) {
                var childName = importNames[i + 1].Name;
                var parentName = importNames[i].Name;
                var parent = new PythonPackage(parentName, Eval.Services);
                parent.AddChildModule(childName, child);
                child = parent;
            }

            if (pythonPackage == null) {
                Eval.DeclareVariable(importNames[0].Name, child, VariableSource.Import, importNames[0]);
            } else {
                pythonPackage.AddChildModule(importNames[existingDepth].Name, child);
            }
        }

        private void MakeUnresolvedImport(string variableName, string moduleName, LocationInfo location) {
            if (!string.IsNullOrEmpty(variableName)) {
                Eval.DeclareVariable(variableName, new SentinelModule(moduleName, Eval.Services), VariableSource.Import, location);
            }
            Eval.ReportDiagnostics(Eval.Module.Uri, new DiagnosticsEntry(
                Resources.ErrorUnresolvedImport.FormatInvariant(moduleName), location.Span, ErrorCodes.UnresolvedImport, Severity.Warning));
        }
    }
}
