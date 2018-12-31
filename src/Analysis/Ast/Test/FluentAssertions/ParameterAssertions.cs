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

using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using FluentAssertions.Primitives;
using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis.Tests.FluentAssertions {
    [ExcludeFromCodeCoverage]
    internal class ParameterAssertions : ReferenceTypeAssertions<IParameterInfo, ParameterAssertions> {
        public ParameterAssertions(IParameterInfo p) {
            Subject = p;
        }

        protected override string Identifier => nameof(IParameterInfo);

        public AndWhichConstraint<ParameterAssertions, string> HaveName(string name, string because = "", params object[] reasonArgs) {
            Subject.Name.Should().Be(name, because, reasonArgs);
            return new AndWhichConstraint<ParameterAssertions, string>(this, Subject.Name);
        }

        public AndWhichConstraint<ParameterAssertions, IPythonType> HaveType(string name, string because = "", params object[] reasonArgs) {
            Subject.Type.Should().NotBeNull(because, reasonArgs);
            Subject.Type.Name.Should().Be(name);
            return new AndWhichConstraint<ParameterAssertions, IPythonType>(this, Subject.Type);
        }
        public AndWhichConstraint<ParameterAssertions, IPythonType> HaveType(BuiltinTypeId typeId, string because = "", params object[] reasonArgs) {
            Subject.Type.Should().NotBeNull(because, reasonArgs);
            Subject.Type.TypeId.Should().Be(typeId, because, reasonArgs);
            return new AndWhichConstraint<ParameterAssertions, IPythonType>(this, Subject.Type);
        }

        public AndWhichConstraint<ParameterAssertions, IParameterInfo> HaveNoDefaultValue(string because = "", params object[] reasonArgs) {
            Subject.DefaultValueString.Should().BeNull(because, reasonArgs);
            return new AndWhichConstraint<ParameterAssertions, IParameterInfo>(this, Subject);
        }
    }
}
