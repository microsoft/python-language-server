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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Analyzer.Modules {
    internal sealed class LazyPythonModule : PythonModule, ILazyModule {
        private IPythonModule _module;

        public LazyPythonModule(string moduleName, IServiceContainer services) :
            base(moduleName, ModuleType.Library, services) { }

        public override string Documentation => MaybeModule?.Documentation ?? string.Empty;
        public override IEnumerable<LocationInfo> Locations => MaybeModule?.Locations.MaybeEnumerate();

        private IPythonModule MaybeModule => Volatile.Read(ref _module);

        private IPythonModule GetModule() {
            var module = Volatile.Read(ref _module);
            if (module != null) {
                return module;
            }

            module = Interpreter.ModuleResolution.ImportModule(Name);
            return SetModule(module);
        }

        public override IEnumerable<string> GetChildrenModuleNames() => GetModule().GetChildrenModuleNames();
        public override IPythonType GetMember(string name) => GetModule().GetMember(name);
        public override IEnumerable<string> GetMemberNames() => GetModule().GetMemberNames();

        public async Task LoadAsync(CancellationToken cancellationToken = default) {
            var module = Volatile.Read(ref _module);
            if (module == null) {
                module = await Interpreter.ModuleResolution.ImportModuleAsync(Name, cancellationToken);
                SetModule(module);
            }
        }

        private IPythonModule SetModule(IPythonModule module) {
            if (module != null) {
                Debug.Assert(!(module is LazyPythonModule), "ImportModule should not return lazy module.");
            }
            module = module ?? new SentinelModule(Name);
            return Interlocked.CompareExchange(ref _module, module, null) ?? module;
        }
    }
}
