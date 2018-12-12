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

namespace Microsoft.Python.Analysis.Analyzer.Types {
    internal sealed class MultipleTypeTypes : PythonMultipleTypes, IPythonType {
        public MultipleTypeTypes(IPythonType[] members) : base(members) { }

        public override string Name => ChooseName(Types.Select(t => t.Name)) ?? "<type>";
        public override string Documentation => ChooseDocumentation(Types.Select(t => t.Documentation));
        public override BuiltinTypeId TypeId => Types.GroupBy(t => t.TypeId).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key ?? BuiltinTypeId.Unknown;
        public override IPythonModule DeclaringModule => CreateAs<IPythonModule>(Types.Select(t => t.DeclaringModule));
        public override bool IsBuiltin => Types.All(t => t.IsBuiltin);
        public override bool IsTypeFactory => Types.All(t => t.IsTypeFactory);
        public override IPythonType GetMember(string name) => Create(Types.Select(t => t.GetMember(name)));
        public override IEnumerable<string> GetMemberNames() => Types.SelectMany(t => t.GetMemberNames()).Distinct();
        public override PythonMemberType MemberType => PythonMemberType.Class;
        public override IPythonFunction GetConstructor() => CreateAs<IPythonFunction>(Types.Select(t => t.GetConstructor()));
    }
}
