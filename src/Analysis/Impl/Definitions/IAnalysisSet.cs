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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.


using System.Collections.Generic;

namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// Represents an unordered collection of <see cref="AnalysisValue" /> objects;
    /// in effect, a set of Python types. There are multiple implementing
    /// classes, including <see cref="AnalysisValue" /> itself, to improve memory
    /// usage for small sets.
    /// 
    /// <see cref="AnalysisSet" /> does not implement this interface, but
    /// provides factory and extension methods.
    /// </summary>
    public interface IAnalysisSet : IEnumerable<AnalysisValue> {
        IAnalysisSet Add(AnalysisValue item, bool canMutate = false);
        IAnalysisSet Add(AnalysisValue item, out bool wasChanged, bool canMutate = false);
        IAnalysisSet Union(IEnumerable<AnalysisValue> items, bool canMutate = false);
        IAnalysisSet Union(IEnumerable<AnalysisValue> items, out bool wasChanged, bool canMutate = false);
        IAnalysisSet Clone();

        bool Contains(AnalysisValue item);
        bool SetEquals(IAnalysisSet other);

        int Count { get; }
        IEqualityComparer<AnalysisValue> Comparer { get; }
    }
}
