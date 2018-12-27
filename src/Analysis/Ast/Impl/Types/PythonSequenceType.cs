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
    internal class PythonSequenceType : PythonTypeWrapper, IPythonSequenceType {
        public PythonSequenceType(BuiltinTypeId typeId, IPythonInterpreter interpreter)
            : base(interpreter.GetBuiltinType(typeId), interpreter.ModuleResolution.BuiltinsModule) { }

        public IMember GetValueAt(IPythonInstance instance, int index) 
            => (instance as IPythonSequence)?.GetValueAt(index) ?? DeclaringModule.Interpreter.GetBuiltinType(BuiltinTypeId.Unknown);

        public IEnumerable<IMember> GetContents(IPythonInstance instance) 
            => (instance as IPythonSequence)?.GetContents() ?? Enumerable.Empty<IMember>();

        public IPythonIterator GetIterator(IPythonInstance instance) 
            => (instance as IPythonSequence)?.GetIterator();

        public override PythonMemberType MemberType => PythonMemberType.Class;
        public override IMember GetMember(string name) 
            => name == @"__iter__" ? new PythonIteratorType(TypeId, DeclaringModule) : base.GetMember(name);
    }
}
