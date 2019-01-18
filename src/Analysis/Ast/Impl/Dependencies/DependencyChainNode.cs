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
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core.Diagnostics;

namespace Microsoft.Python.Analysis.Dependencies {
    internal sealed class DependencyChainNode : IDependencyChainNode {
        public DependencyChainNode(IDocument document, IEnumerable<IDependencyChainNode> children = null) {
            Check.InvalidOperation(() => document is IAnalyzable, "Document must be analyzable entity");

            Document = document;
            SnapshotVersion = Analyzable.ExpectedAnalysisVersion;
            Children = children ?? Enumerable.Empty<IDependencyChainNode>();
        }

        /// <summary>
        /// Analyzable object (usually the document itself).
        /// </summary>
        public IAnalyzable Analyzable => (IAnalyzable)Document;

        /// <summary>
        /// Document to analyze.
        /// </summary>
        public IDocument Document { get; }

        /// <summary>
        /// Object snapshot version at the time of the dependency chain creation.
        /// Used to track if completed analysis version matches the current snapshot.
        /// </summary>
        public int SnapshotVersion { get; }

        /// <summary>
        /// Dependent documents to analyze after this one. Child chains
        /// can be analyzed concurrently.
        /// </summary>
        public IEnumerable<IDependencyChainNode> Children { get; }
    }
}
