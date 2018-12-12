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

using System.Collections.Generic;
using System.Linq;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Analyzer.Types {
    /// <summary>
    /// Delegates most of the methods to the wrapped/inner class.
    /// </summary>
    internal class PythonTypeWrapper : PythonType {
        protected IPythonType InnerType { get; }

        public PythonTypeWrapper(IPythonType type)
            : this(type, type.DeclaringModule) { }

        public PythonTypeWrapper(IPythonType type, IPythonModule declaringModule)
            : base(type?.Name ?? "<type wrapper>", declaringModule, type?.Documentation,
             (type as ILocatedMember)?.Locations.MaybeEnumerate().FirstOrDefault()) {
            InnerType = type;
        }

        public override BuiltinTypeId TypeId => InnerType?.TypeId ?? BuiltinTypeId.Unknown;
        public override PythonMemberType MemberType => InnerType?.MemberType ?? PythonMemberType.Unknown;
        public override IPythonType GetMember(string name) => InnerType?.GetMember(name);
        public override IEnumerable<string> GetMemberNames() => InnerType?.GetMemberNames();

    }
}
