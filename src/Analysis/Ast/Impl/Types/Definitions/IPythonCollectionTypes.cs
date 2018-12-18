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

namespace Microsoft.Python.Analysis.Types {
    public interface IPythonIterable : IPythonType {
        IPythonIterator IteratorType { get; }
    }

    public interface IPythonIterator : IPythonType {
        IEnumerable<IPythonType> NextType { get; }
    }

    public interface IPythonSequence : IPythonType {
        IEnumerable<IPythonType> IndexTypes { get; }
    }

    public interface IPythonLookup : IPythonType {
        IEnumerable<IPythonType> KeyTypes { get; }
        IEnumerable<IPythonType> ValueTypes { get; }
        IEnumerable<IPythonType> GetIndex(IPythonType key);
    }
}
