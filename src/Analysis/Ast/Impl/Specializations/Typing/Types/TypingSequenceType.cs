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
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Specializations.Typing.Types {
    internal sealed class TypingSequenceType : PythonSequenceType {
        public TypingSequenceType(string name, BuiltinTypeId typeId, IPythonModule declaringModule, IPythonType contentType)
            : base(name, typeId, declaringModule.Interpreter, declaringModule, contentType) {
            Name = $"{name}[{contentType.Name}]";
        }

        public static IPythonType Create(string name, BuiltinTypeId typeId, IPythonModule declaringModule, IReadOnlyList<IPythonType> typeArguments) {
            if (typeArguments.Count == 1) {
                return new TypingSequenceType(name, typeId, declaringModule, typeArguments[0]);
            }
            // TODO: report wrong number of arguments
            return null;
        }

        public override IMember GetValueAt(IPythonInstance instance, int index) => ContentType;
        public override IEnumerable<IMember> GetContents(IPythonInstance instance) => Enumerable.Repeat(ContentType, 1);

        public override string Name { get; }
    }
}
