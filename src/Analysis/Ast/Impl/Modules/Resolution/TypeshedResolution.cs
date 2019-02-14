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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Modules.Resolution {
    internal sealed class TypeshedResolution : ModuleResolutionBase, IModuleResolution {
        private readonly IReadOnlyList<string> _typeStubPaths;

        public TypeshedResolution(IServiceContainer services) : base(null, services) {
            _modules[BuiltinModuleName] = BuiltinsModule = _interpreter.ModuleResolution.BuiltinsModule;
            _root = _interpreter.Configuration?.TypeshedPath;
            // TODO: merge with user-provided stub paths
            _typeStubPaths = GetTypeShedPaths(_interpreter.Configuration?.TypeshedPath).ToArray();

            _log?.Log(TraceEventType.Verbose, @"Typeshed paths:");
            foreach (var p in _typeStubPaths) {
                _log?.Log(TraceEventType.Verbose, $"    {p}");
            }
        }

        internal Task InitializeAsync(CancellationToken cancellationToken = default)
            => ReloadAsync(cancellationToken);

        protected override async Task<IPythonModule> DoImportAsync(string name, CancellationToken cancellationToken = default) {
            var mp = FindModuleInSearchPath(_typeStubPaths, null, name);
            if (mp != null) {
                if (mp.Value.IsCompiled) {
                    _log?.Log(TraceEventType.Warning, "Unsupported native module in stubs", mp.Value.FullName, mp.Value.SourceFile);
                    return null;
                }
                return await CreateStubModuleAsync(mp.Value.FullName, mp.Value.SourceFile, cancellationToken);
            }

            var i = name.IndexOf('.');
            if (i == 0) {
                Debug.Fail("Invalid module name");
                return null;
            }

            var stubPath = CurrentPathResolver.GetPossibleModuleStubPaths(name).FirstOrDefault(p => _fs.FileExists(p));
            return stubPath != null ? await CreateStubModuleAsync(name, stubPath, cancellationToken) : null;
        }

        public override Task ReloadAsync(CancellationToken cancellationToken = default) {
            _pathResolver = new PathResolver(_interpreter.LanguageVersion);

            var addedRoots = _pathResolver.SetRoot(_root);
            ReloadModulePaths(addedRoots);

            addedRoots = _pathResolver.SetInterpreterSearchPaths(_typeStubPaths);
            ReloadModulePaths(addedRoots);

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

        private ModulePath? FindModuleInSearchPath(IReadOnlyList<string> searchPaths, IReadOnlyDictionary<string, string> packages, string name) {
            if (searchPaths == null || searchPaths.Count == 0) {
                return null;
            }

            var i = name.IndexOf('.');
            var firstBit = i < 0 ? name : name.Remove(i);

            ModulePath mp;
            Func<string, bool> isPackage = IsPackage;
            if (firstBit.EndsWithOrdinal("-stubs", ignoreCase: true)) {
                isPackage = _fs.DirectoryExists;
            }

            var requireInitPy = ModulePath.PythonVersionRequiresInitPyFiles(Configuration.Version);
            if (packages != null && packages.TryGetValue(firstBit, out var searchPath) && !string.IsNullOrEmpty(searchPath)) {
                if (ModulePath.FromBasePathAndName_NoThrow(searchPath, name, isPackage, null, requireInitPy, out mp, out _, out _, out _)) {
                    return mp;
                }
            }

            if (searchPaths.MaybeEnumerate()
                .Any(sp => ModulePath.FromBasePathAndName_NoThrow(sp, name, isPackage, null, requireInitPy, out mp, out _, out _, out _))) {
                return mp;
            }
            return null;
        }

        /// <summary>
        /// Determines whether the specified directory is an importable package.
        /// </summary>
        private bool IsPackage(string directory)
            => ModulePath.PythonVersionRequiresInitPyFiles(Configuration.Version) ?
                !string.IsNullOrEmpty(ModulePath.GetPackageInitPy(directory)) :
                _fs.DirectoryExists(directory);

        protected override void ReportModuleNotFound(string name)
            => _log?.Log(TraceEventType.Verbose, $"Typeshed stub not found for '{name}'");
    }
}
