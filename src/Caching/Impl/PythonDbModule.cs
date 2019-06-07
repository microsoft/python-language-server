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
using System.Linq;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Caching {
    internal sealed class PythonDbModule : SpecializedModule {
        private readonly ModuleModel _model;
        private readonly IMember _unknownType;

        public PythonDbModule(ModuleModel model, IServiceContainer services) 
            : base(model.Name, string.Empty, services) {
            _model = model;
            _unknownType = services.GetService<IPythonInterpreter>().UnknownType;
        }

        protected override string LoadContent() => string.Empty;

        public override IEnumerable<string> GetMemberNames() {
            var classes = _model.Classes.Select(c => c.Name);
            var functions = _model.Functions.Select(c => c.Name);
            var variables = _model.Variables.Select(c => c.Name);
            return classes.Concat(functions).Concat(variables);
        }

        public override IMember GetMember(string name) {
            var v = _model.Variables.FirstOrDefault(c => c.Name == name);
            if(v != null) {
                return new Variable(name, Construct(v.Value), VariableSource.Declaration, new Location(this));
            }

            return _unknownType;
        }

        private IMember Construct(string qualifiedName) {
            return null;
        }
    }
}
