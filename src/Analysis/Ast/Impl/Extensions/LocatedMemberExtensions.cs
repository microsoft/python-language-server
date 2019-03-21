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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis {
    public static class LocatedMemberExtensions {
        public static bool HasLocation(this ILocatedMember lm) 
            => lm.DeclaringModule == null && lm.Definition != null;

        public static LocationInfo GetDefinitionLocation(this ILocatedMember lm, PythonAst ast)
            => lm.HasLocation() ? lm.Definition.GetLocation(ast) : LocationInfo.Empty;

        public static IReadOnlyList<LocationInfo> GetReferenceLocations(this ILocatedMember lm, PythonAst ast)
            => lm.References.Select(r => r.GetLocation(ast)).ToArray();
    }
}
