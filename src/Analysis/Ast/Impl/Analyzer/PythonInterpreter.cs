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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Shell;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.Analysis.Analyzer {
    /// <summary>
    /// Describes Python interpreter associated with the analysis.
    /// </summary>
    internal sealed class PythonInterpreter : IPythonInterpreter {
        private ModuleResolution _moduleResolution;
        private readonly object _lock = new object();
        private readonly Dictionary<BuiltinTypeId, IPythonType> _builtinTypes = new Dictionary<BuiltinTypeId, IPythonType>() {
            { BuiltinTypeId.NoneType, new PythonType("NoneType", BuiltinTypeId.NoneType) },
            { BuiltinTypeId.Unknown, new PythonType("Unknown", BuiltinTypeId.Unknown) }
        };

        private PythonInterpreter(InterpreterConfiguration configuration) {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            LanguageVersion = Configuration.Version.ToLanguageVersion();
        }

        public static async Task<IPythonInterpreter> CreateAsync(InterpreterConfiguration configuration, string root, IServiceManager sm, CancellationToken cancellationToken = default) {
            var pi = new PythonInterpreter(configuration);
            sm.AddService(pi);
            pi._moduleResolution = new ModuleResolution(root, sm);
            await pi._moduleResolution.LoadBuiltinTypesAsync(cancellationToken);
            return pi;
        }

        /// <summary>
        /// Interpreter configuration.
        /// </summary>
        public InterpreterConfiguration Configuration { get; }

        /// <summary>
        /// Python language version.
        /// </summary>
        public PythonLanguageVersion LanguageVersion { get; }

        /// <summary>
        /// Module resolution service.
        /// </summary>
        public IModuleResolution ModuleResolution => _moduleResolution;

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

            lock (_lock) {
                if (_builtinTypes.TryGetValue(id, out var res) && res != null) {
                    return res;
                }

                var bm = ModuleResolution.BuiltinsModule;
                var typeName = id.GetTypeName(LanguageVersion);
                if (typeName != null) {
                    res = bm.GetMember(typeName) as IPythonType;
                }

                if (res == null) {
                    res = bm.GetAnyMember("__{0}__".FormatInvariant(id)) as IPythonType;
                    if (res == null) {
                        return _builtinTypes[BuiltinTypeId.Unknown];
                    }
                }

                _builtinTypes[id] = res;
                return res;
            }
        }

        public void NotifyImportableModulesChanged() => ModuleResolution.ReloadAsync().DoNotWait();
    }
}
