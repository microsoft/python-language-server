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
using Microsoft.Python.Analysis.Types.Collections;

namespace Microsoft.Python.Analysis.Values.Collections {
    /// <summary>
    /// Iterator that uses custom definitions of __iter__
    /// and __next__ in the supplied instance type.
    /// </summary>
    internal sealed class PythonInstanceIterator : PythonInstance, IPythonIterator {
        private readonly IPythonFunctionType __next__;

        public PythonInstanceIterator(IMember instance, IPythonInterpreter interpreter)
            : base(new PythonIteratorType(BuiltinTypeId.SetIterator, interpreter)) {
            __next__ = instance.GetPythonType().GetMember(@"__next__") as IPythonFunctionType;
        }

        public IMember Next => __next__?.Call(null, @"__next__", ArgumentSet.Empty) ?? UnknownType;

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
