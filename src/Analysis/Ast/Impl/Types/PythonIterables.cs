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
            IEnumerable<IMember> contents,
            IPythonType iteratorBase,
            IPythonModule declaringModule
        ) : base(iterableType, declaringModule) {
            Iterator = new PythonIterator(iteratorBase, contents, declaringModule);
        }

        public IPythonIterator Iterator { get; }
    }

    internal sealed class PythonIterator : PythonTypeWrapper, IPythonIterator, IPythonIterable {

        public PythonIterator(IPythonType iterableType, IEnumerable<IMember> contents, IPythonModule declaringModule):
            base(iterableType, declaringModule) {
            // TODO: handle non-homogenous collections
            Next = contents.FirstOrDefault();
        }

        public IPythonIterator Iterator => this;
        public IMember Next { get; }
    }
}
