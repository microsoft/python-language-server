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


using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Protocol;

namespace Microsoft.Python.LanguageServer.Sources {
    internal sealed class RenameSource {
        private readonly IServiceContainer _services;
        private readonly string _rootPath;

        public RenameSource(IServiceContainer services, string rootPath) {
            _services = services;
            _rootPath = rootPath;
        }

        public async Task<WorkspaceEdit> RenameAsync(Uri uri, SourceLocation location, string newName, CancellationToken cancellationToken = default) {
            var rs = new ReferenceSource(_services, _rootPath);
            var references = await rs.FindAllReferencesAsync(uri, location, ReferenceSearchOptions.ExcludeLibraries, cancellationToken);
            if (references.Length == 0) {
                return null;
            }

            var changes = new Dictionary<Uri, List<TextEdit>>();
            foreach (var r in references) {
                if (!changes.TryGetValue(r.uri, out var edits)) {
                    changes[r.uri] = edits = new List<TextEdit>();
                }
                edits.Add(new TextEdit { newText = newName, range = r.range });
            }

            return new WorkspaceEdit {
                changes = changes.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray())
            };
        }
    }
}
