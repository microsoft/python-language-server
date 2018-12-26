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
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Types {
    public interface IPythonIterable : IPythonType {
        IPythonIterator Iterator { get; }
    }

    /// <summary>
    /// Represents iterator that can enumerate items in a set.
    /// </summary>
    public interface IPythonIterator : IPythonType {
        IMember Next { get; }
    }

    /// <summary>
    /// Represents type that has values at indexes,
    /// such as list or array.
    /// </summary>
    public interface IPythonSequence : IPythonType {
        IMember GetValueAt(IPythonInstance instance, int index);
        IEnumerable<IMember> GetContents(IPythonInstance instance);
    }

    /// <summary>
    /// Represents dictionary-like type, such as tuple.
    /// </summary>
    public interface IPythonLookup : IPythonType {
        IEnumerable<IMember> Keys { get; }
        IEnumerable<IMember> Values { get; }
        IEnumerable<IMember> GetAt(IMember key);
    }
}
