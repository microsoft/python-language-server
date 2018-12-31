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
using Microsoft.Python.Analysis.Utilities;

namespace Microsoft.Python.Analysis.Specializations.Typing.Types {
    internal class TypedSequenceType : PythonSequenceType, ITypedSequenceType {
        public TypedSequenceType(
            string name,
            BuiltinTypeId typeId,
            IPythonModule declaringModule,
            IPythonType contentType,
            bool isMutable
            ) : base(name, typeId, declaringModule, contentType, isMutable) {
            Name = $"{name}[{contentType.Name}]";
            ContentTypes = new[] { contentType };
        }

        public TypedSequenceType(
            string name,
            BuiltinTypeId typeId,
            IPythonModule declaringModule,
            IReadOnlyList<IPythonType> contentTypes,
            bool isMutable
        ) : base(name, typeId, declaringModule, contentTypes, isMutable) {
            Name = CodeFormatter.FormatSequence(name, '[', contentTypes);
            ContentTypes = contentTypes;
        }

        public override string Name { get; }

        /// <summary>
        /// Sequence types.
        /// </summary>
        public IReadOnlyList<IPythonType> ContentTypes { get; }
    }
}
