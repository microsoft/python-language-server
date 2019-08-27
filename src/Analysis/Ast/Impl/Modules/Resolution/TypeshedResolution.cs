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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Core.IO;

namespace Microsoft.Python.Analysis.Modules.Resolution {
    internal sealed class TypeshedResolution : ModuleResolutionBase, IModuleResolution {
        private readonly ImmutableArray<string> _typeStubPaths;

        public TypeshedResolution(IServiceContainer services, string root = null) : base(null, services) {
            BuiltinsModule = _interpreter.ModuleResolution.BuiltinsModule;
            Modules[BuiltinModuleName] = new ModuleRef(BuiltinsModule);

            var stubs = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Stubs");
            Root = root;
            // TODO: merge with user-provided stub paths
            _typeStubPaths = GetTypeShedPaths(Root)
                .Concat(GetTypeShedPaths(stubs))
                .Where(services.GetService<IFileSystem>().DirectoryExists)
                .ToImmutableArray();

            _log?.Log(TraceEventType.Verbose, @"Typeshed paths:");
            foreach (var p in _typeStubPaths) {
                _log?.Log(TraceEventType.Verbose, $"    {p}");
            }
        }

        internal Task InitializeAsync(CancellationToken cancellationToken = default)
            => ReloadAsync(cancellationToken);

        protected override IPythonModule CreateModule(string name) {
            var moduleImport = CurrentPathResolver.GetModuleImportFromModuleName(name);
            if (moduleImport != default) {
                if (moduleImport.IsCompiled) {
                    _log?.Log(TraceEventType.Warning, "Unsupported native module in stubs", moduleImport.FullName, moduleImport.ModulePath);
                    return null;
                }
                return new StubPythonModule(moduleImport.FullName, moduleImport.ModulePath, true, _services);
            }

            var i = name.IndexOf('.');
            if (i == 0) {
                Debug.Fail("Invalid module name");
                return null;
            }

            var stubPath = CurrentPathResolver.GetPossibleModuleStubPaths(name).FirstOrDefault(p => _fs.FileExists(p));
            return stubPath != null ? new StubPythonModule(name, stubPath, true, _services) : null;
        }

        public Task ReloadAsync(CancellationToken cancellationToken = default) {
            Modules.Clear();
            PathResolver = new PathResolver(_interpreter.LanguageVersion, Root, _typeStubPaths, ImmutableArray<string>.Empty);
            ReloadModulePaths(_typeStubPaths.Prepend(Root));
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        private IEnumerable<string> GetTypeShedPaths(string typeshedRootPath) {
            if (string.IsNullOrEmpty(typeshedRootPath)) {
                yield break;
            }

            var stdlib = Path.Combine(typeshedRootPath, "stdlib");
            var thirdParty = Path.Combine(typeshedRootPath, "third_party");

            var v = Configuration.Version;
            var subdirs = new List<string> { v.Major.ToString(), "2and3" };
            for (var i = 1; i < v.Minor; i++) {
                subdirs.Add($"{v.Major}.{i}");
            }

            // For 3: all between 3 and current version inclusively + 2and3
            foreach (var subdir in subdirs) {
                yield return Path.Combine(stdlib, subdir);
            }

            foreach (var subdir in subdirs) {
                yield return Path.Combine(thirdParty, subdir);
            }
        }
    }
}
