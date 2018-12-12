﻿// Copyright(c) Microsoft Corporation
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
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Dependencies {
    internal sealed class DependencyResolver : IDependencyResolver {
        public DependencyResolver(IServiceContainer services) {
        }
        public Task<IDependencyChainNode> GetDependencyChainAsync(IDocument document, CancellationToken cancellationToken) {
            // TODO: implement
            return Task.FromResult<IDependencyChainNode>(new DependencyChainNode(document));
        }
    }
}
