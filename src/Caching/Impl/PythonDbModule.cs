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
using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Caching {
    internal sealed class PythonDbModule : SpecializedModule {
        private readonly GlobalScope _globalScope;
        private readonly IPythonInterpreter _interpreter;

        public PythonDbModule(ModuleModel model, IServiceContainer services)
            : base(model.Name, string.Empty, services) {

            _globalScope = new GlobalScope(model, this, services);
            Documentation = model.Documentation;
        }

        protected override string LoadContent() => string.Empty;

        public override string Documentation { get; }
        public override IEnumerable<string> GetMemberNames() => _globalScope.Variables.Names;
        public override IMember GetMember(string name) => _globalScope.Variables[name];
        public override IGlobalScope GlobalScope => _globalScope;
    }
}
