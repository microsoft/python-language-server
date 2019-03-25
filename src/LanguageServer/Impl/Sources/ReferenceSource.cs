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

using System.Linq;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Protocol;

namespace Microsoft.Python.LanguageServer.Sources {
    internal sealed class ReferenceSource {
        public Reference[] FindAllReferences(IDocumentAnalysis analysis, ILocatedMember definingMember, SourceLocation location) {
            // Get to the root definition
            for (; definingMember.Parent != null; definingMember = definingMember.Parent) { }
            // Basic, single-file case
            return definingMember.References
                .Select(r => new Reference { uri = analysis.Document.Uri, range = r.Span })
                .ToArray();
            // TODO: locate in closed files.
        }
    }
}
