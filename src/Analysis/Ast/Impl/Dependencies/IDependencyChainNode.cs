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

namespace Microsoft.Python.Analysis.Dependencies {
    internal interface IDependencyChainNode<out TValue> {
        int VertexDepth { get; }

        /// <summary>
        /// Returns true if node has any direct or indirect dependencies that aren't added to the graph, otherwise false
        /// </summary>
        bool HasMissingDependencies { get; }
        /// <summary>
        /// Returns true if node has only direct and indirect dependencies that have been walked at least once, otherwise false
        /// </summary>
        bool HasOnlyWalkedDependencies { get; }
        /// <summary>
        /// Returns true if node has been walked and all its direct and indirect dependencies have been walked, otherwise false
        /// </summary>
        bool IsWalkedWithDependencies { get; }
        /// <summary>
        /// Returns true if node version matches version of the walked graph
        /// </summary>
        bool IsValidVersion { get; }
        TValue Value { get; }
        void MarkWalked();
        void MoveNext();
    }
}
