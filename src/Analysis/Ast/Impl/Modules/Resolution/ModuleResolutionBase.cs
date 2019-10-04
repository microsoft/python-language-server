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
using Microsoft.Python.Analysis.Caching;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Core.Services;

namespace Microsoft.Python.Analysis.Modules.Resolution {
    internal abstract class ModuleResolutionBase {
        protected IServiceContainer Services { get; }
        protected IFileSystem FileSystem { get; }
        protected IPythonInterpreter Interpreter { get; }
        protected ILogger Log { get; }

        protected ConcurrentDictionary<string, ModuleRef> Modules { get; } = new ConcurrentDictionary<string, ModuleRef>();
        protected PathResolver PathResolver { get; set; }

        protected InterpreterConfiguration Configuration => Interpreter.Configuration;

        public string Root { get; }
        public IStubCache StubCache { get; }

        protected ModuleResolutionBase(string root, IServiceContainer services) {
            Root = root;
            Services = services;
            FileSystem = services.GetService<IFileSystem>();

            Interpreter = services.GetService<IPythonInterpreter>();
            StubCache = services.GetService<IStubCache>();
            Log = services.GetService<ILogger>();
        }

        public ImmutableArray<string> InterpreterPaths { get; protected set; } = ImmutableArray<string>.Empty;
        public ImmutableArray<string> UserPaths { get; protected set; } = ImmutableArray<string>.Empty;

        /// <summary>
        /// Path resolver providing file resolution in module imports.
        /// </summary>
        public PathResolverSnapshot CurrentPathResolver => PathResolver.CurrentSnapshot;

        protected abstract IPythonModule CreateModule(string name);

        public IPythonModule GetImportedModule(string name) {
            if (name == Interpreter.ModuleResolution.BuiltinsModule.Name) {
                return Interpreter.ModuleResolution.BuiltinsModule;
            }
            return Modules.TryGetValue(name, out var moduleRef)
                ? moduleRef.Value
                : Interpreter.ModuleResolution.GetSpecializedModule(name);
        }

        public IPythonModule GetOrLoadModule(string name) {
            // Specialized should always win. However, we don't want
            // to allow loading from the database just yet since module
            // may already exist in the analyzed state.
            var module = GetImportedModule(name);
            if (module != null) {
                return module;
            }

            module = Interpreter.ModuleResolution.GetSpecializedModule(name);
            if (module != null) {
                return module;
            }

            // Now try regular case.
            if (Modules.TryGetValue(name, out var moduleRef)) {
                return moduleRef.GetOrCreate(name, this);
            }

            moduleRef = Modules.GetOrAdd(name, new ModuleRef());
            return moduleRef.GetOrCreate(name, this);
        }

        public ModulePath FindModule(string filePath) {
            var bestLibraryPath = string.Empty;

            foreach (var p in InterpreterPaths.Concat(UserPaths)) {
                if (PathEqualityComparer.Instance.StartsWith(filePath, p)) {
                    if (p.Length > bestLibraryPath.Length) {
                        bestLibraryPath = p;
                    }
                }
            }
            return ModulePath.FromFullPath(filePath, bestLibraryPath);
        }

        protected void ReloadModulePaths(in IEnumerable<string> rootPaths) {
            foreach (var root in rootPaths) {
                foreach (var moduleFile in PathUtils.EnumerateFiles(FileSystem, root)) {
                    PathResolver.TryAddModulePath(moduleFile.FullName, moduleFile.Length, allowNonRooted: false, out _);
                }

                if (PathUtils.TryGetZipFilePath(root, out var zipFilePath, out var _) && File.Exists(zipFilePath)) {
                    foreach (var moduleFile in PathUtils.EnumerateZip(zipFilePath)) {
                        if (!PathUtils.PathStartsWith(moduleFile.FullName, "EGG-INFO")) {
                            PathResolver.TryAddModulePath(
                                Path.Combine(zipFilePath,
                                PathUtils.NormalizePath(moduleFile.FullName)),
                                moduleFile.Length, allowNonRooted: false, out _
                            );
                        }
                    }
                }
            }
        }
        protected class ModuleRef {
            private readonly object _syncObj = new object();
            private IPythonModule _module;

            public ModuleRef(IPythonModule module) {
                _module = module;
            }

            public ModuleRef() { }

            public IPythonModule Value {
                get {
                    lock (_syncObj) {
                        return _module;
                    }
                }
            }

            public IPythonModule GetOrCreate(string name, ModuleResolutionBase mrb) {
                lock (_syncObj) {
                    if (_module != null) {
                        return _module;
                    }

                    var module = mrb.CreateModule(name);
                    _module = module;
                    return module;
                }
            }
        }
    }
}
