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

using System.Collections.Generic;
using System.Linq;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Text;

namespace Microsoft.Python.Analysis.Analyzer {
    internal sealed class EmptyAnalysis : IDocumentAnalysis {
        public EmptyAnalysis(IDocument document = null) {
            Document = document;
            GlobalScope = new EmptyGlobalScope(document);
        }

        public IDocument Document { get; }
        public IGlobalScope GlobalScope { get; }
        public IEnumerable<IPythonType> GetAllAvailableItems(SourceLocation location) => Enumerable.Empty<IPythonType>();
        public IReadOnlyDictionary<string, IPythonType> Members => EmptyDictionary<string, IPythonType>.Instance;
        public IEnumerable<IPythonType> GetMembers(SourceLocation location) => Enumerable.Empty<IPythonType>();
        public IEnumerable<IPythonFunctionOverload> GetSignatures(SourceLocation location) => Enumerable.Empty<IPythonFunctionOverload>();
        public IEnumerable<IPythonType> GetValues(SourceLocation location) => Enumerable.Empty<IPythonType>();
    }
}
