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

using Microsoft.Python.Analysis.Types;
using System.Collections.Generic;

namespace Microsoft.Python.Analysis.Values.Collections {
    /// <summary>
    /// Collection iterator.
    /// </summary>
    internal class PythonIterator : PythonInstance, IPythonIterator {
        private int _index;

        protected IPythonCollection Collection { get; }

        public PythonIterator(BuiltinTypeId iteratorTypeId, IPythonCollection collection) 
            : base(collection.Type.DeclaringModule.Interpreter.GetBuiltinType(iteratorTypeId)) {
            Collection = collection;
        }

        protected PythonIterator(IPythonType iteratorTypeId) : base(iteratorTypeId) { }

        public virtual IMember Next => Collection.Index(GetArgSet(_index++)) ?? UnknownType;

        private IArgumentSet GetArgSet(int index) {
            var newArg = new PythonConstant(index, Type.DeclaringModule.Interpreter.GetBuiltinType(BuiltinTypeId.Int));
            return new ArgumentSet(new List<IMember> { newArg }, null, null);
        }

        public override IMember Call(string memberName, IArgumentSet args) {
            // Specializations
            switch (memberName) {
                case @"__next__":
                case @"next":
                    return Next;
            }
            return base.Call(memberName, args);
        }
    }
}
