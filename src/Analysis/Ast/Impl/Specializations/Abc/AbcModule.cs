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
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Specializations.Typing {
    internal sealed class AbcModule : SpecializedModule {
        private readonly Dictionary<string, IMember> _members = new Dictionary<string, IMember>();

        private AbcModule(string modulePath, IServiceContainer services) : base("abc", modulePath, services) { }

        public static IPythonModule Create(IServiceContainer services) {
            var interpreter = services.GetService<IPythonInterpreter>();
            var module = interpreter.ModuleResolution
                .SpecializeModule("abc", modulePath => new AbcModule(modulePath, services)) as AbcModule;
            module?.SpecializeMembers();
            return module;
        }

        #region IMemberContainer
        public override IMember GetMember(string name) => _members.TryGetValue(name, out var m) ? m : null;
        public override IEnumerable<string> GetMemberNames() => _members.Keys;
        #endregion

        private void SpecializeMembers() {
            // ABC
            var cls = PythonClassType.Specialize("abc", this, GetMemberDocumentation("abc"), isAbstract: true);
            _members["ABC"] = cls;
        }

        private string GetMemberDocumentation(string name)
        => base.GetMember(name)?.GetPythonType()?.Documentation;
    }
}
