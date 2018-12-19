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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.IO;

namespace Microsoft.Python.Analysis.Modules {
    /// <summary>
    /// Represents builtins module. Typically generated for a given Python interpreter
    /// by running special 'scraper' Python script that generates Python code via
    /// introspection of the compiled built-in language types.
    /// </summary>
    internal sealed class BuiltinsPythonModule : CompiledPythonModule, IBuiltinPythonModule {
        private readonly HashSet<string> _hiddenNames = new HashSet<string>();

        public BuiltinsPythonModule(string moduleName, string filePath, IServiceContainer services)
            : base(moduleName, ModuleType.Builtins, filePath, null, services) { } // TODO: builtins stub

        public override IPythonType GetMember(string name) => _hiddenNames.Contains(name) ? null : base.GetMember(name);

        public IPythonType GetAnyMember(string name) => base.GetMember(name);

        public override IEnumerable<string> GetMemberNames() => base.GetMemberNames().Except(_hiddenNames).ToArray();

        protected override IEnumerable<string> GetScrapeArguments(IPythonInterpreter interpreter)
            => !InstallPath.TryGetFile("scrape_module.py", out var sb) ? null : new List<string> { "-B", "-E", sb };


        protected override void OnAnalysisComplete(GlobalScope gs) {
            IPythonType boolType = null;
            IPythonType noneType = null;

            foreach (BuiltinTypeId typeId in Enum.GetValues(typeof(BuiltinTypeId))) {
                var m = GetMember("__{0}__".FormatInvariant(typeId));
                if (m is PythonType biType && biType.IsBuiltin) {
                    if (typeId != BuiltinTypeId.Str && typeId != BuiltinTypeId.StrIterator) {
                        biType.TrySetTypeId(typeId);
                    }

                    if (biType.IsHidden) {
                        _hiddenNames.Add(biType.Name);
                    }
                    _hiddenNames.Add("__{0}__".FormatInvariant(typeId));

                    switch (typeId) {
                        case BuiltinTypeId.Bool:
                            boolType = boolType ?? biType;
                            break;
                        case BuiltinTypeId.NoneType:
                            noneType = noneType ?? biType;
                            break;
                    }
                }
            }
            _hiddenNames.Add("__builtin_module_names__");

            if (boolType != null) {
                gs.DeclareVariable("True", boolType, LocationInfo.Empty);
                gs.DeclareVariable("False", boolType, LocationInfo.Empty);
            }

            if (noneType != null) {
                gs.DeclareVariable("None", noneType, LocationInfo.Empty);
            }
        }
    }
}
