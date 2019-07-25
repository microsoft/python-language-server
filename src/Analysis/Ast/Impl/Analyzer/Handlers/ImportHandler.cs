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
using System.Linq;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;
using ErrorCodes = Microsoft.Python.Analysis.Diagnostics.ErrorCodes;

namespace Microsoft.Python.Analysis.Analyzer.Handlers {
    internal sealed partial class ImportHandler : StatementHandler {
        private readonly Dictionary<string, PythonVariableModule> _variableModules = new Dictionary<string, PythonVariableModule>();

        public ImportHandler(AnalysisWalker walker) : base(walker) { }

        public bool HandleImport(ImportStatement node) {
            if (Module.ModuleType == ModuleType.Specialized) {
                return false;
            }

            var len = Math.Min(node.Names.Count, node.AsNames.Count);
            var forceAbsolute = node.ForceAbsolute;
            for (var i = 0; i < len; i++) {
                var moduleImportExpression = node.Names[i];
                var asNameExpression = node.AsNames[i];
                HandleImport(moduleImportExpression, asNameExpression, forceAbsolute);
            }

            return false;
        }

        private void HandleImport(ModuleName moduleImportExpression, NameExpression asNameExpression, bool forceAbsolute) {
            FindModuleByAbsoluteName(moduleImportExpression, asNameExpression, forceAbsolute, out var firstModule, out var lastModule, out var importNames, out _);
            // "import fob.oar.baz as baz" is handled as baz = import_module('fob.oar.baz')
            // "import fob.oar.baz" is handled as fob = import_module('fob')
            if (!string.IsNullOrEmpty(asNameExpression?.Name) && lastModule != default) {
                Eval.DeclareVariable(asNameExpression.Name, lastModule, VariableSource.Import, asNameExpression);
            } else if (firstModule != default && !string.IsNullOrEmpty(importNames[0])) {
                var firstName = moduleImportExpression.Names[0];
                Eval.DeclareVariable(importNames[0], firstModule, VariableSource.Import, firstName);
            }
        }

        private void FindModuleByAbsoluteName(ModuleName moduleImportExpression, NameExpression asNameExpression, bool forceAbsolute,
            out PythonVariableModule firstModule, out PythonVariableModule lastModule, out ImmutableArray<string> importNames, out IImportSearchResult imports) {
            // "import fob.oar.baz" means
            // import_module('fob')
            // import_module('fob.oar')
            // import_module('fob.oar.baz')
            importNames = ImmutableArray<string>.Empty;
            lastModule = default;
            firstModule = default;
            imports = null;
            foreach (var nameExpression in moduleImportExpression.Names) {
                importNames = importNames.Add(nameExpression.Name);
                imports = ModuleResolution.CurrentPathResolver.GetImportsFromAbsoluteName(Module.FilePath, importNames, forceAbsolute);
                if (!HandleImportSearchResult(imports, lastModule, asNameExpression, moduleImportExpression, out lastModule)) {
                    lastModule = default;
                    break;
                }

                if (firstModule == default) {
                    firstModule = lastModule;
                }
            }
        }

        private bool HandleImportSearchResult(in IImportSearchResult imports, in PythonVariableModule parent, in NameExpression asNameExpression, in Node location, out PythonVariableModule variableModule) {
            switch (imports) {
                case ModuleImport moduleImport when Module.ModuleType == ModuleType.Stub && moduleImport.FullName == Module.Name:
                    return TryGetModuleFromSelf(parent, moduleImport.Name, out variableModule);
                case ModuleImport moduleImport:
                    return TryGetModuleFromImport(moduleImport, parent, location, out variableModule);
                case PossibleModuleImport possibleModuleImport:
                    return TryGetModulePossibleImport(possibleModuleImport, parent, location, out variableModule);
                case ImplicitPackageImport packageImport:
                    return TryGetPackageFromImport(packageImport, parent, out variableModule);
                case RelativeImportBeyondTopLevel importBeyondTopLevel:
                    var message = Resources.ErrorRelativeImportBeyondTopLevel.FormatInvariant(importBeyondTopLevel.RelativeImportName);
                    Eval.ReportDiagnostics(Eval.Module.Uri, 
                        new DiagnosticsEntry(message, location.GetLocation(Eval).Span, ErrorCodes.UnresolvedImport, Severity.Warning, DiagnosticSource.Analysis));
                    variableModule = default;
                    return false;
                case ImportNotFound importNotFound:
                    var memberName = asNameExpression?.Name ?? importNotFound.FullName;
                    MakeUnresolvedImport(memberName, importNotFound.FullName, location);
                    variableModule = default;
                    return false;
                default:
                    variableModule = default;
                    return false;
            }
        }

        private bool TryGetModuleFromSelf(in PythonVariableModule parent, in string memberName, out PythonVariableModule variableModule) {
            variableModule = GetOrCreateVariableModule(Module, parent, memberName);
            return true;
        }

        private bool TryGetModuleFromImport(in ModuleImport moduleImport, in PythonVariableModule parent, Node location, out PythonVariableModule variableModule) {
            var module = ModuleResolution.GetOrLoadModule(moduleImport.FullName);
            if (module != null) {
                variableModule = GetOrCreateVariableModule(module, parent, moduleImport.Name);
                return true;
            }

            MakeUnresolvedImport(moduleImport.FullName, moduleImport.FullName, location);
            variableModule = default;
            return false;
        }

        private bool TryGetModulePossibleImport(PossibleModuleImport possibleModuleImport, PythonVariableModule parent, Node location, out PythonVariableModule variableModule) {
            if (_variableModules.TryGetValue(possibleModuleImport.PossibleModuleFullName, out variableModule)) {
                return true;
            }

            var fullName = possibleModuleImport.PrecedingModuleFullName;
            var module = ModuleResolution.GetOrLoadModule(possibleModuleImport.PrecedingModuleFullName);

            if (module == default) {
                MakeUnresolvedImport(possibleModuleImport.PrecedingModuleFullName, fullName, location);
                return false;
            }

            variableModule = GetOrCreateVariableModule(module, parent, possibleModuleImport.PrecedingModuleName);
            var nameParts = possibleModuleImport.RemainingNameParts;
            for (var i = 0; i < nameParts.Count; i++) {
                var namePart = nameParts[i];
                var member = variableModule.GetMember(namePart);
                switch (member) {
                    case PythonVariableModule childVariableModule:
                        variableModule = childVariableModule.Module != null
                            ? GetOrCreateVariableModule(childVariableModule.Module, variableModule, namePart)
                            : GetOrCreateVariableModule(childVariableModule.Name, variableModule, namePart);
                        break;
                    case IPythonModule childModule:
                        variableModule = GetOrCreateVariableModule(childModule, variableModule, namePart);
                        break;
                    default:
                        var unresolvedModuleName = string.Join(".", nameParts.Take(i + 1).Prepend(fullName));
                        MakeUnresolvedImport(unresolvedModuleName, fullName, location);
                        return false;
                }
            }
            
            return true;
        }

        private bool TryGetPackageFromImport(in ImplicitPackageImport implicitPackageImport, in PythonVariableModule parentModule, out PythonVariableModule variableModule) {
            variableModule = GetOrCreateVariableModule(implicitPackageImport.FullName, parentModule, implicitPackageImport.Name);
            return true;
        }

        private void MakeUnresolvedImport(string variableName, string moduleName, Node location) {
            if (!string.IsNullOrEmpty(variableName)) {
                Eval.DeclareVariable(variableName, new SentinelModule(moduleName, Eval.Services), VariableSource.Import, location);
            }
            Eval.ReportDiagnostics(Eval.Module.Uri, 
                new DiagnosticsEntry(Resources.ErrorUnresolvedImport.FormatInvariant(moduleName), 
                    Eval.GetLocationInfo(location).Span, ErrorCodes.UnresolvedImport, Severity.Warning, DiagnosticSource.Analysis));
        }

        private PythonVariableModule GetOrCreateVariableModule(in string fullName, in PythonVariableModule parentModule, in string memberName) {
            if (_variableModules.TryGetValue(fullName, out var variableModule)) {
                return variableModule;
            }

            variableModule = new PythonVariableModule(fullName, Eval.Interpreter);
            _variableModules[fullName] = variableModule;
            parentModule?.AddChildModule(memberName, variableModule);
            return variableModule;
        }

        private PythonVariableModule GetOrCreateVariableModule(in IPythonModule module, in PythonVariableModule parentModule, in string memberName) {
            var moduleFullName = module.Name;
            if (_variableModules.TryGetValue(moduleFullName, out var variableModule)) {
                return variableModule;
            }

            variableModule = new PythonVariableModule(module);
            _variableModules[moduleFullName] = variableModule;
            parentModule?.AddChildModule(memberName, variableModule);
            return variableModule;
        }
    }
}
