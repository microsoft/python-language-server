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

namespace Microsoft.Python.Analysis.Specializations.Typing.Types {
    internal sealed class TypingSequenceType: PythonSequenceType, ITypingSequenceType {
        public TypingSequenceType(IPythonType contentType, IPythonModuleType declaringModule) 
            : base(BuiltinTypeId.List, declaringModule.Interpreter, declaringModule) {
            ContentType = contentType;
        }

        public static IPythonType Create(IReadOnlyList<IPythonType> typeArguments, IPythonModuleType declaringModule) {
            if (typeArguments.Count == 1) {
                return new TypingSequenceType(typeArguments[0], declaringModule);
            }
            // TODO: report wrong number of arguments
            return null;
        }

        public IPythonType ContentType { get; }

    }
}
