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

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Core.Collections;

namespace Microsoft.Python.Analysis.Dependencies {
    internal interface IDependencyChainWalker<TKey, TValue> {
        ImmutableArray<TKey> MissingKeys { get; }
        ImmutableArray<TValue> AffectedValues { get; }
        int Version { get; }
        int Remaining { get; }
        Task<IDependencyChainNode<TValue>> GetNextAsync(CancellationToken cancellationToken);
    }
}
