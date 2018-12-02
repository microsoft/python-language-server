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

namespace Microsoft.Python.Analysis {
    public interface IPythonIterableType : IPythonType {
        IPythonIteratorType IteratorType { get; }
    }

    public interface IPythonIteratorType : IPythonType {
        IEnumerable<IPythonType> NextType { get; }
    }

    public interface IPythonSequenceType : IPythonType {
        IEnumerable<IPythonType> IndexTypes { get; }
    }

    public interface IPythonLookupType : IPythonType {
        IEnumerable<IPythonType> KeyTypes { get; }
        IEnumerable<IPythonType> ValueTypes { get; }
        IEnumerable<IPythonType> GetIndex(IPythonType key);
    }
}
