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
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Analysis.Values.Collections;

namespace Microsoft.Python.Analysis.Specializations.Builtins {
    /// <summary>
    /// Represents object returned from enumerate().
    /// </summary>
    internal sealed class PythonEnumerator : PythonIterator, IPythonEnumerator {
        private readonly IPythonModule _declaringModule;
        private readonly IPythonIterator _iterator;

        public PythonEnumerator(IArgumentSet argSet, IPythonModule declaringModule)
            : base(declaringModule.Interpreter.GetBuiltinType(BuiltinTypeId.TupleIterator)) {
            _declaringModule = declaringModule;
            var args = argSet.Values<IMember>();
            if (args.Count > 0) {
                _iterator = (args[0] as IPythonIterable)?.GetIterator();
            }
        }

        public IMember IndexValue
            => _declaringModule.Interpreter.GetBuiltinType(BuiltinTypeId.Int).CreateInstance(ArgumentSet.WithoutContext);
        public override IMember Next
            => _iterator?.Next ?? _declaringModule.Interpreter.UnknownType.CreateInstance(ArgumentSet.WithoutContext);
    }
}
