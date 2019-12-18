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

using Microsoft.Python.Core.Collections;

namespace Microsoft.Python.Analysis.Dependencies {
    /// <summary>
    /// Represents subsystem that can resolve module dependencies for analysis.
    /// Given module the subsystem can return ordered chain of dependencies 
    /// for the analysis. The chain is a tree where child branches can be analyzed
    /// concurrently.
    /// </summary>
    internal interface IDependencyResolver<TKey, TValue> {
        int TryAddValue(in TKey key, in TValue value, in bool isRoot, in ImmutableArray<TKey> incomingKeys);
        int ChangeValue(in TKey key, in TValue value, in bool isRoot, in ImmutableArray<TKey> incomingKeys);
        int Remove(in TKey key);
        void Reset();

        IDependencyChainWalker<TKey, TValue> CreateWalker();
        bool TryCreateWalker(in int version, in int walkerDepthLimit, out IDependencyChainWalker<TKey, TValue> walker);
    }
}
