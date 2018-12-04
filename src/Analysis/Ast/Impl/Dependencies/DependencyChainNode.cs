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
using Microsoft.Python.Analysis.Documents;

namespace Microsoft.Python.Analysis.Dependencies {
    internal sealed class DependencyChainNode : IDependencyChainNode {
        public DependencyChainNode(IDocument document, IEnumerable<IDependencyChainNode> children = null) {
            Document = document;
            DocumentVersion = document.Version;
            Children = children ?? Enumerable.Empty<IDependencyChainNode>();
        }

        /// <summary>
        /// Document to analyze.
        /// </summary>
        public IDocument Document { get; }

        /// <summary>
        /// Version of the document at the time of the dependency chain creation.
        /// Used to track if completed analysis matches current document snapshot.
        /// </summary>
        public int DocumentVersion { get; }

        /// <summary>
        /// Dependent documents to analyze after this one. Child chains
        /// can be analyzed concurrently.
        /// </summary>
        public IEnumerable<IDependencyChainNode> Children { get; }
    }
}
