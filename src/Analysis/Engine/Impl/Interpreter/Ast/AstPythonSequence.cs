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

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonSequence : AstPythonTypeWrapper, IPythonSequenceType, IPythonIterableType {
        public AstPythonSequence(
            IPythonType sequenceType,
            IPythonModule declaringModule,
            IEnumerable<IPythonType> contents,
            IPythonType iteratorBase
        ): base(sequenceType, declaringModule) {
            IndexTypes = (contents ?? throw new ArgumentNullException(nameof(contents))).ToArray();
            IteratorType = new AstPythonIterator(iteratorBase, IndexTypes, declaringModule);
        }

        public IEnumerable<IPythonType> IndexTypes { get; }
        public IPythonIteratorType IteratorType { get; }
 
        public override string Name => InnerType?.Name ?? "tuple";
        public override BuiltinTypeId TypeId => InnerType?.TypeId ?? BuiltinTypeId.Tuple;
        public override bool IsBuiltIn => InnerType?.IsBuiltIn ?? true;
        public override PythonMemberType MemberType => InnerType?.MemberType ?? PythonMemberType.Class;
    }
}
