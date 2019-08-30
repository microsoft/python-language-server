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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Modules.Resolution;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Services;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.Analysis.Analyzer {
    /// <summary>
    /// Describes Python interpreter associated with the analysis.
    /// </summary>
    public sealed class PythonInterpreter : IPythonInterpreter {
        private MainModuleResolution _moduleResolution;
        private TypeshedResolution _stubResolution;
        private IPythonType _unknownType;
        private readonly object _lock = new object();

        private readonly Dictionary<BuiltinTypeId, IPythonType> _builtinTypes = new Dictionary<BuiltinTypeId, IPythonType>();

        private PythonInterpreter(InterpreterConfiguration configuration) {
            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            LanguageVersion = Configuration.Version.ToLanguageVersion();
        }

        private async Task InitializeAsync(string root, IServiceManager sm, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();

            sm.AddService(this);
            _moduleResolution = new MainModuleResolution(root, sm);
            _stubResolution = new TypeshedResolution(Configuration.TypeshedPath, sm);
            
            await _stubResolution.ReloadAsync(cancellationToken);
            await _moduleResolution.ReloadAsync(cancellationToken);
        }

        public static async Task<IPythonInterpreter> CreateAsync(InterpreterConfiguration configuration, string root, IServiceManager sm, CancellationToken cancellationToken = default) {
            var pi = new PythonInterpreter(configuration);
            await pi.InitializeAsync(root, sm, cancellationToken);

            // Specialize typing
            TypingModule.Create(sm);
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
        public IModuleManagement ModuleResolution => _moduleResolution;

        /// <summary>
        /// Stub resolution service.
        /// </summary>
        public IModuleResolution TypeshedResolution => _stubResolution;

        /// <summary>
        /// Unknown type.
        /// </summary>
        public IPythonType UnknownType {
            get {
                lock (_lock) {
                    var type = _unknownType;
                    if (type != null) {
                        return type;
                    }

                    _unknownType = new PythonType("Unknown", new Location(_moduleResolution.BuiltinsModule), string.Empty);
                    _builtinTypes[BuiltinTypeId.Unknown] = _unknownType;
                    return _unknownType;
                }
            }
        }

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
                if (id == BuiltinTypeId.Unknown) {
                    return UnknownType;
                }

                if (_builtinTypes.TryGetValue(id, out var type) && type != null) {
                    return type;
                }

                if (id == BuiltinTypeId.NoneType) {
                    type = new PythonType("NoneType", new Location(_moduleResolution.BuiltinsModule), string.Empty, BuiltinTypeId.NoneType);
                } else {
                    var bm = _moduleResolution.BuiltinsModule;
                    var typeName = id.GetTypeName(LanguageVersion);
                    if (typeName != null) {
                        type = _moduleResolution.BuiltinsModule.GetMember(typeName) as IPythonType;
                    }

                    if (type == null) {
                        type = bm.GetAnyMember("__{0}__".FormatInvariant(id)) as IPythonType;
                        if (type == null) {
                            return UnknownType;
                        }
                    }
                }

                _builtinTypes[id] = type;
                return type;
            }
        }
    }
}
