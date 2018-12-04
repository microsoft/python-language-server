// Python Tools for Visual Studio
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
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Infrastructure;

namespace Microsoft.PythonTools.Interpreter.Ast {
    internal sealed class AstNestedPythonModule : PythonModuleType, IPythonModule, ILocatedMember {
        private readonly IPythonInterpreter _interpreter;
        private IPythonModule _module;

        public AstNestedPythonModule(IPythonInterpreter interpreter, string fullName) : base(fullName) {
            _interpreter = interpreter ?? throw new ArgumentNullException(nameof(interpreter));
        }

        public override string Documentation => MaybeModule?.Documentation ?? string.Empty;
        public IEnumerable<ILocationInfo> Locations => ((MaybeModule as ILocatedMember)?.Locations).MaybeEnumerate();

        public bool IsLoaded => MaybeModule != null;
        private IPythonModule MaybeModule => Volatile.Read(ref _module);

        private IPythonModule GetModule() {
            var module = Volatile.Read(ref _module);
            if (module != null) {
                return module;
            }

            module = _interpreter.ImportModule(Name);
            if (module != null) {
                Debug.Assert(!(module is AstNestedPythonModule), "ImportModule should not return nested module");
            }

            if (module == null) {
                module = new SentinelModule(Name, false);
            }

            return Interlocked.CompareExchange(ref _module, module, null) ?? module;
        }

        public IEnumerable<string> GetChildrenModules() => GetModule().GetChildrenModules();

        public override IMember GetMember(IModuleContext context, string name)
            => GetModule().GetMember(context, name);

        public override IEnumerable<string> GetMemberNames(IModuleContext context) =>
            // TODO: Make GetMemberNames() faster than Imported()
            GetModule().GetMemberNames(context);

        public void Imported(IModuleContext context) => GetModule().Imported(context);
    }
}
