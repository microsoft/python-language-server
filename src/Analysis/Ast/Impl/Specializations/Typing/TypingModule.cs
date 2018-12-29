﻿// Copyright(c) Microsoft Corporation
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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Specializations.Typing.Types;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Specializations.Typing {
    internal sealed class TypingModule : SpecializedModule {
        private readonly Dictionary<string, IMember> _members = new Dictionary<string, IMember>();

        private TypingModule(string modulePath, IServiceContainer services) : base("typing", modulePath, services) { }

        public static async Task<IPythonModule> CreateAsync(IServiceContainer services, CancellationToken cancellationToken = default) {
            var interpreter = services.GetService<IPythonInterpreter>();
            var module = await interpreter.ModuleResolution
                .SpecializeModuleAsync("typing", modulePath => new TypingModule(modulePath, services), cancellationToken) as TypingModule;
            module?.SpecializeMembers();
            return module;
        }

        #region IMemberContainer
        public override IMember GetMember(string name) => _members.TryGetValue(name, out var m) ? m : null;
        public override IEnumerable<string> GetMemberNames() => _members.Keys;
        #endregion

        private void SpecializeMembers() {
            // TypeVar
            var fn = new PythonFunctionType("TypeVar", this, null, GetMemberDocumentation, GetMemberLocation);
            var o = new PythonFunctionOverload(fn.Name, Enumerable.Empty<IParameterInfo>(), _ => fn.Location);
            o.AddReturnValue(new PythonTypeDeclaration("TypeVar", this));
            fn.AddOverload(o);
            _members["TypeVar"] = fn;

            _members["List"] = new GenericType("List", this, 
                (typeArgs, module, location) => TypingSequenceType.Create("List", BuiltinTypeId.List, module, typeArgs));
        }


        private string GetMemberDocumentation(string name)
            => base.GetMember(name)?.GetPythonType()?.Documentation;
        private LocationInfo GetMemberLocation(string name)
            => (base.GetMember(name)?.GetPythonType() as ILocatedMember)?.Location ?? LocationInfo.Empty;
    }
}
