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

using System.Collections.Generic;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Dependencies {
    internal sealed class DependencyCollector {
        private readonly IPythonModule _module;
        private readonly bool _isTypeshed;
        private readonly IModuleManagement _moduleResolution;
        private readonly PathResolverSnapshot _pathResolver;

        public HashSet<AnalysisModuleKey> Dependencies { get; } = new HashSet<AnalysisModuleKey>();

        public DependencyCollector(IPythonModule module) {
            _module = module;
            _isTypeshed = module is StubPythonModule stub && stub.IsTypeshed;
            _moduleResolution = module.Interpreter.ModuleResolution;
            _pathResolver = _isTypeshed
                ? module.Interpreter.TypeshedResolution.CurrentPathResolver
                : _moduleResolution.CurrentPathResolver;

            if (module.Stub != null) {
                Dependencies.Add(new AnalysisModuleKey(module.Stub));
            }
        }
        
        public void AddImport(IReadOnlyList<string> importNames, bool forceAbsolute) {
            var imports = _pathResolver.GetImportsFromAbsoluteName(_module.FilePath, importNames, forceAbsolute);
            HandleSearchResults(imports);
        }

        public void AddFromImport(IReadOnlyList<string> importNames, int dotCount, bool forceAbsolute) {
            var imports = _pathResolver.FindImports(_module.FilePath, importNames, dotCount, forceAbsolute);
            HandleSearchResults(imports);
            if (imports is IImportChildrenSource childrenSource) {
                foreach (var name in importNames) {
                    if (childrenSource.TryGetChildImport(name, out var childImport)) {
                        HandleSearchResults(childImport);
                    }
                }
            }
        }

        private void HandleSearchResults(IImportSearchResult searchResult) {
            switch (searchResult) {
                case ModuleImport moduleImport when !Ignore(_moduleResolution, moduleImport.FullName, moduleImport.ModulePath):
                    Dependencies.Add(new AnalysisModuleKey(moduleImport.FullName, moduleImport.ModulePath, _isTypeshed));
                    return;
                case PossibleModuleImport possibleModuleImport when !Ignore(_moduleResolution, possibleModuleImport.PrecedingModuleFullName, possibleModuleImport.PrecedingModulePath):
                    Dependencies.Add(new AnalysisModuleKey(possibleModuleImport.PrecedingModuleFullName, possibleModuleImport.PrecedingModulePath, _isTypeshed));
                    return;
                default:
                    return;
            }
        }
        private static bool Ignore(IModuleManagement moduleResolution, string fullName, string modulePath)
            => moduleResolution.BuiltinModuleName.EqualsOrdinal(fullName) || moduleResolution.IsSpecializedModule(fullName, modulePath);
    }
}
