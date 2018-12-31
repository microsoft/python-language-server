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
using Microsoft.Python.Analysis.Specializations.Typing.Values;
using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis.Specializations.Typing.Types {
    internal class TypingTupleType : TypingSequenceType {
        public TypingTupleType(IPythonModule declaringModule, IReadOnlyList<IPythonType> contentTypes)
            : base("Tuple", BuiltinTypeId.Tuple, declaringModule, contentTypes, false) { }

        public static IPythonType Create(
            IPythonModule declaringModule,
            IReadOnlyList<IPythonType> typeArguments
        ) {
            if (typeArguments.Count > 0) {
                return new TypingTupleType(declaringModule, typeArguments);
            }
            // TODO: report wrong number of arguments
            return declaringModule.Interpreter.UnknownType;
        }

        public override IMember CreateInstance(IPythonModule declaringModule, LocationInfo location, params object[] args)
            // TODO: report mismatch between type arguments and initialization arguments
            => new TypingTuple(this, location);
    }
}
