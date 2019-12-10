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

namespace Microsoft.Python.Analysis.Caching {
    public enum ObjectType {
        Type,
        Instance,
        Module,
        VariableModule,
        BuiltinModule
    }

    internal struct QualifiedNameParts {
        /// <summary>Object type.</summary>
        public ObjectType ObjectType;
        /// <summary>Module name.</summary>
        public string ModuleName;
        /// <summary>Indicates if module is a stub.</summary>
        public bool IsStub;
        /// <summary>Module unique id.</summary>
        public string ModuleId;
        /// <summary>Module member names such as 'A', 'B', 'C' from module:A.B.C.</summary>
        public IReadOnlyList<string> MemberNames;
    }
}
