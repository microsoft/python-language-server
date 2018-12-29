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
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Types {
    /// <summary>
    /// Type info for a sequence.
    /// </summary>
    internal class PythonSequenceType : PythonTypeWrapper, IPythonSequenceType {
        private readonly PythonIteratorType _iteratorType;

        /// <summary>
        /// Creates type info for a sequence.
        /// </summary>
        /// <param name="sequenceTypeId">Sequence type id, such as <see cref="BuiltinTypeId.List"/>.</param>
        /// <param name="interpreter">Python interpreter</param>
        /// <param name="declaringModule">Declaring module. Can be null of module is 'builtins'.</param>
        public PythonSequenceType(BuiltinTypeId sequenceTypeId, IPythonInterpreter interpreter, IPythonModuleType declaringModule = null)
            : base(interpreter.GetBuiltinType(sequenceTypeId),
                declaringModule ?? interpreter.ModuleResolution.BuiltinsModule) {
            _iteratorType = new PythonIteratorType(sequenceTypeId.GetIteratorTypeId(), DeclaringModule);
        }

        /// <summary>
        /// Retrieves value at a given index for specific instance.
        /// Equivalent to the <see cref="PythonSequence.GetValueAt"/>.
        /// </summary>
        public IMember GetValueAt(IPythonInstance instance, int index) 
            => (instance as IPythonSequence)?.GetValueAt(index) ?? DeclaringModule.Interpreter.GetBuiltinType(BuiltinTypeId.Unknown);

        public IEnumerable<IMember> GetContents(IPythonInstance instance) 
            => (instance as IPythonSequence)?.GetContents() ?? Enumerable.Empty<IMember>();

        public IPythonIterator GetIterator(IPythonInstance instance) 
            => (instance as IPythonSequence)?.GetIterator();

        public override PythonMemberType MemberType => PythonMemberType.Class;
        public override IMember GetMember(string name) => name == @"__iter__" ? _iteratorType : base.GetMember(name);
    }
}
