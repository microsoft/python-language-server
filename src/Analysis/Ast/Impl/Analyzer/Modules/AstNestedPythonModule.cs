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
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Python.Analysis.Analyzer.Types;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Analyzer.Modules {
    internal sealed class AstNestedPythonModule : PythonModuleType, ILocatedMember {
        private IPythonModule _module;

        public AstNestedPythonModule(string fullName, IPythonInterpreter interpreter)
            : base(fullName, interpreter) {
        }

        public override string Documentation => MaybeModule?.Documentation ?? string.Empty;
        public IEnumerable<LocationInfo> Locations => ((MaybeModule as ILocatedMember)?.Locations).MaybeEnumerate();

        private IPythonModule MaybeModule => Volatile.Read(ref _module);

        private IPythonModule GetModule() {
            var module = Volatile.Read(ref _module);
            if (module != null) {
                return module;
            }

            module = Interpreter.ModuleResolution.ImportModule(Name);
            if (module != null) {
                Debug.Assert(!(module is AstNestedPythonModule), "ImportModule should not return nested module");
            }

            module = module ?? new SentinelModule(Name, false);
            return Interlocked.CompareExchange(ref _module, module, null) ?? module;
        }

        public override IEnumerable<string> GetChildrenModuleNames() => GetModule().GetChildrenModuleNames();
        public override IMember GetMember(string name) => GetModule().GetMember(name);

        public override IEnumerable<string> GetMemberNames() => GetModule().GetMemberNames();
        public override void LoadAndAnalyze() => GetModule().LoadAndAnalyze();
        internal override string GetCode() => (GetModule() as PythonModuleType)?.GetCode() ?? string.Empty;
    }
}
