﻿// Python Tools for Visual Studio
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
using System.Linq;
using System.Threading;
using Microsoft.Python.Core;
using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstNestedPythonModule : PythonModuleType, IPythonModule, ILocatedMember {
        private IPythonModule _module;
        private readonly IPythonInterpreter _interpreter;
        private readonly IReadOnlyList<string> _importNames;

        public AstNestedPythonModule(
            IPythonInterpreter interpreter,
            string name,
            IReadOnlyList<string> importNames
        ) : base(name) {
            _interpreter = interpreter ?? throw new ArgumentNullException(nameof(interpreter));
            _importNames = importNames ?? throw new ArgumentNullException(nameof(importNames));
        }

        public override string Documentation => MaybeModule?.Documentation ?? string.Empty;
        public IEnumerable<ILocationInfo> Locations => ((MaybeModule as ILocatedMember)?.Locations).MaybeEnumerate();

        public bool IsLoaded => MaybeModule != null;
        private IPythonModule MaybeModule => Volatile.Read(ref _module);

        private IPythonModule GetModule() {
            var mod = Volatile.Read(ref _module);
            if (mod != null) {
                return mod;
            }

            foreach (var n in _importNames) {
                mod = _interpreter.ImportModule(n);
                if (mod != null) {
                    Debug.Assert(!(mod is AstNestedPythonModule), "ImportModule should not return nested module");
                    break;
                }
            }
            if (mod == null) {
                mod = new SentinelModule(_importNames.FirstOrDefault() ?? "<unknown>", false);
            }

            return Interlocked.CompareExchange(ref _module, mod, null) ?? mod;
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
