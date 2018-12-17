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

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Python.Analysis.Types {
    internal class PythonIterable : PythonTypeWrapper, IPythonIterable {
        public PythonIterable(
            IPythonType iterableType,
            IEnumerable<IPythonType> types,
            IPythonType iteratorBase,
            IPythonModule declaringModule
        ) : base(iterableType, declaringModule) {
            IteratorType = new AstPythonIterator(iteratorBase, types, declaringModule);
        }

        public IPythonIterator IteratorType { get; }
    }

    class AstPythonIterator : PythonTypeWrapper, IPythonIterator, IPythonIterable {
        private readonly IPythonType _type;

        public AstPythonIterator(IPythonType iterableType, IEnumerable<IPythonType> nextType, IPythonModule declaringModule):
            base(iterableType, declaringModule) {
            _type = iterableType;
            NextType = nextType.ToArray();
        }

        public IPythonIterator IteratorType => this;
        public IEnumerable<IPythonType> NextType { get; }
    }
}
