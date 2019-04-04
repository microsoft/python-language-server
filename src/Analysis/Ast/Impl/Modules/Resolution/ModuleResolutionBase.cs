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

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Core.Services;

namespace Microsoft.Python.Analysis.Modules.Resolution {
    internal abstract class ModuleResolutionBase {
        protected readonly IServiceContainer _services;
        protected readonly IPythonInterpreter _interpreter;
        protected readonly IFileSystem _fs;
        protected readonly ILogger _log;
        protected readonly IUIService _ui;
        protected readonly bool _requireInitPy;
        protected string _root;

        protected ConcurrentDictionary<string, ModuleRef> Modules { get; } = new ConcurrentDictionary<string, ModuleRef>();
        protected PathResolver PathResolver { get; set; }

        protected InterpreterConfiguration Configuration => _interpreter.Configuration;

        protected ModuleResolutionBase(string root, IServiceContainer services) {
            _root = root;
            _services = services;
            _interpreter = services.GetService<IPythonInterpreter>();
            _fs = services.GetService<IFileSystem>();
            _log = services.GetService<ILogger>();
            _ui = services.GetService<IUIService>();

            _requireInitPy = ModulePath.PythonVersionRequiresInitPyFiles(_interpreter.Configuration.Version);
        }

        public IModuleCache ModuleCache { get; protected set; }
        public string BuiltinModuleName => BuiltinTypeId.Unknown.GetModuleName(_interpreter.LanguageVersion);

        /// <summary>
        /// Path resolver providing file resolution in module imports.
        /// </summary>
        public PathResolverSnapshot CurrentPathResolver => PathResolver.CurrentSnapshot;

        /// <summary>
        /// Builtins module.
        /// </summary>
        public IBuiltinsPythonModule BuiltinsModule { get; protected set; }

        protected abstract IPythonModule CreateModule(string name);

        public IReadOnlyCollection<string> GetPackagesFromDirectory(string searchPath, CancellationToken cancellationToken) {
            return ModulePath.GetModulesInPath(
                searchPath,
                recurse: false,
                includePackages: true,
                requireInitPy: _requireInitPy
            ).Select(mp => mp.ModuleName).Where(n => !string.IsNullOrEmpty(n)).TakeWhile(_ => !cancellationToken.IsCancellationRequested).ToList();
        }

        public IPythonModule GetImportedModule(string name) 
            => Modules.TryGetValue(name, out var moduleRef) ? moduleRef.Value : _interpreter.ModuleResolution.GetSpecializedModule(name);

        public IPythonModule GetOrLoadModule(string name) {
            if (Modules.TryGetValue(name, out var moduleRef)) {
                return moduleRef.GetOrCreate(name, this);
            }

            var module = _interpreter.ModuleResolution.GetSpecializedModule(name);
            if (module != null) {
                return module;
            }

            moduleRef = Modules.GetOrAdd(name, new ModuleRef());
            return moduleRef.GetOrCreate(name, this);
        }

        public bool TryAddModulePath(in string path, out string fullModuleName) 
            => PathResolver.TryAddModulePath(path, true, out fullModuleName);

        public ModulePath FindModule(string filePath) {
            var bestLibraryPath = string.Empty;

            foreach (var p in Configuration.SearchPaths) {
                if (PathEqualityComparer.Instance.StartsWith(filePath, p)) {
                    if (p.Length > bestLibraryPath.Length) {
                        bestLibraryPath = p;
                    }
                }
            }
            return ModulePath.FromFullPath(filePath, bestLibraryPath);
        }

        protected void ReloadModulePaths(in IEnumerable<string> rootPaths) {
            foreach (var modulePath in rootPaths.Where(Directory.Exists).SelectMany(p => PathUtils.EnumerateFiles(p))) {
                PathResolver.TryAddModulePath(modulePath, false, out _);
            }
        }

        protected class ModuleRef {
            private readonly object _syncObj = new object();
            private IPythonModule _module;
            private bool _creating;

            public ModuleRef(IPythonModule module) {
                _module = module;
            }

            public ModuleRef() {}

            public IPythonModule Value {
                get {
                    lock (_syncObj) {
                        return _module;
                    }
                }
            }

            public IPythonModule GetOrCreate(string name, ModuleResolutionBase mrb) {
                bool create = false;
                lock (_syncObj) {
                    if (_module != null) {
                        return _module;
                    }

                    if (!_creating) {
                        create = true;
                        _creating = true;
                    }
                }

                if (!create) {
                    return null;
                }

                var module = mrb.CreateModule(name);
                lock (_syncObj) {
                    _creating = false;
                    _module = module;
                    return module;
                }
            }
        }
    }
}
