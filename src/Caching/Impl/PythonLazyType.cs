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
using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis.Caching {
    internal sealed class PythonLazyType: PythonTypeWrapper {
        private readonly PythonDbModule _dbModule;

        public PythonLazyType(string name, string qualifiedTypeName, string documentation, PythonDbModule dbModule, BuiltinTypeId typeid, ModuleDatabase db) 
            : base(name, documentation, declaringModule, ) {
            _dbModule = dbModule;
            TypeId = typeid;

        }

        public override BuiltinTypeId TypeId { get; }

        public override IEnumerable<string> GetMemberNames() {
            return base.GetMemberNames();
        }

        public override IMember GetMember(string name) {
            return base.GetMember(name);
        }

        private void EnsureInnerType() {
            _dbModule
        }
    }
}
