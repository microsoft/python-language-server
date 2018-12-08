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
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Core.Shell;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.Analysis.Analyzer {
    internal sealed class AstPythonInterpreter : IPythonInterpreter {
        private readonly Dictionary<BuiltinTypeId, IPythonType> _builtinTypes = new Dictionary<BuiltinTypeId, IPythonType>() {
            { BuiltinTypeId.NoneType, new AstPythonType("NoneType", BuiltinTypeId.NoneType) },
            { BuiltinTypeId.Unknown, new AstPythonType("Unknown", BuiltinTypeId.Unknown) }
        };
        
        private readonly object _userSearchPathsLock = new object();

        public AstPythonInterpreter(InterpreterConfiguration configuration, IServiceContainer services) {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            Services = services ?? throw new ArgumentNullException(nameof(services));
            ModuleResolution = new AstModuleResolution(this);
            LanguageVersion = Configuration.Version.ToLanguageVersion();
            Log = services.GetService<ILogger>();
        }

        public void Dispose() { }
        
        /// <summary>
        /// Interpreter configuration.
        /// </summary>
        public InterpreterConfiguration Configuration { get; }

        public IServiceContainer Services { get; }
        public ILogger Log { get; }
        public PythonLanguageVersion LanguageVersion { get; }
        public IModuleResolution ModuleResolution { get; private set; }

        /// <summary>
        /// Gets a well known built-in type such as int, list, dict, etc...
        /// </summary>
        /// <param name="id">The built-in type to get</param>
        /// <returns>An IPythonType representing the type.</returns>
        /// <exception cref="KeyNotFoundException">
        /// The requested type cannot be resolved by this interpreter.
        /// </exception>
        public IPythonType GetBuiltinType(BuiltinTypeId id) {
            if (id < 0 || id > BuiltinTypeIdExtensions.LastTypeId) {
                throw new KeyNotFoundException("(BuiltinTypeId)({0})".FormatInvariant((int)id));
            }

            IPythonType res;
            lock (_builtinTypes) {
                if (!_builtinTypes.TryGetValue(id, out res)) {
                    var bm = ModuleResolution.BuiltinModule;
                    res = bm.GetAnyMember("__{0}__".FormatInvariant(id)) as IPythonType;
                    if (res == null) {
                        var name = id.GetTypeName(Configuration.Version);
                        if (string.IsNullOrEmpty(name)) {
                            Debug.Assert(id == BuiltinTypeId.Unknown, $"no name for {id}");
                            if (!_builtinTypes.TryGetValue(BuiltinTypeId.Unknown, out res)) {
                                _builtinTypes[BuiltinTypeId.Unknown] = res = new AstPythonType("<unknown>", bm, null, null, BuiltinTypeId.Unknown);
                            }
                        } else {
                            res = new AstPythonType(name, bm, null, null, id);
                        }
                    }
                    _builtinTypes[id] = res;
                }
            }
            return res;
        }

        public void NotifyImportableModulesChanged() => ModuleResolution = new AstModuleResolution(this);
    }
}
