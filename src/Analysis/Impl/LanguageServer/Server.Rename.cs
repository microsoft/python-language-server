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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis.Infrastructure;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    public sealed partial class Server {
        public override async Task<WorkspaceEdit> Rename(RenameParams @params, CancellationToken cancellationToken) {
            ProjectFiles.GetAnalysis(@params.textDocument, @params.position, @params._version, out var entry, out var tree);
            if (entry == null || tree == null) {
                throw new InvalidOperationException(Resources.RenameVariable_UnableGetExpressionAnalysis);
            }

            var references = await FindReferences(new ReferencesParams {
                textDocument = new TextDocumentIdentifier { uri = @params.textDocument.uri },
                position = @params.position,
                context = new ReferenceContext { includeDeclaration = true }
            }, cancellationToken);

            if (references.Any(x => x._isModule)) {
                throw new InvalidOperationException(Resources.RenameVariable_CannotRenameModuleName);
            }

            var definition = references.FirstOrDefault(r => r._kind == ReferenceKind.Definition);
            if (definition.uri == null) {
                throw new InvalidOperationException(Resources.RenameVariable_CannotRename);
            }

            var definitionSpan = definition.range.ToLinearSpan(tree);
            var reader = new DocumentReader(entry as IDocument, ProjectFiles.GetPart(definition.uri));
            var originalName = reader.Read(definitionSpan.Start, definitionSpan.Length);
            if (originalName == null) {
                throw new InvalidOperationException(Resources.RenameVariable_SelectSymbol);
            }
            if (!references.Any(r => r._kind == ReferenceKind.Definition || r._kind == ReferenceKind.Reference)) {
                throw new InvalidOperationException(Resources.RenameVariable_NoInformationAvailableForVariable.FormatUI(originalName));
            }

            // See https://en.wikipedia.org/wiki/Name_mangling, Python section.
            var privatePrefix = entry.Analysis.GetPrivatePrefix(definition.range.start);
            if (!string.IsNullOrEmpty(privatePrefix) && !string.IsNullOrEmpty(originalName) && originalName.StartsWithOrdinal(privatePrefix)) {
                originalName = originalName.Substring(privatePrefix.Length + 1);
            }

            // Group by URI for more optimal document reading in FilterPrivatePrefixed
            var grouped = references
                .GroupBy(x => x.uri)
                .ToDictionary(g => g.Key, e => e.ToList());

            var refs = FilterPrivatePrefixed(grouped, originalName, privatePrefix, @params.newName, @params.textDocument.uri, reader);
            // Convert to Dictionary<Uri, TextEdit[]>
            var changes = refs
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Select(t => new TextEdit {
                        range = t.range,
                        newText = @params.newName
                    }).ToArray());

            return new WorkspaceEdit { changes = changes };
        }

        private Dictionary<Uri, List<Reference>> FilterPrivatePrefixed(
            Dictionary<Uri, List<Reference>> refs,
            string originalName,
            string privatePrefix,
            string newName,
            Uri documentReaderUri,
            IDocumentReader documentReader) {
            // Filter out references depending on the private prefix, if any.
            foreach (var kvp in refs) {
                var ent = ProjectFiles.GetEntry(kvp.Key);

                var ast = (ent as ProjectEntry).GetCurrentParse().Tree;
                var reader = kvp.Key == documentReaderUri
                    ? documentReader
                    : new DocumentReader(ent as IDocument, ProjectFiles.GetPart(kvp.Key));

                if (ast == null || reader == null) {
                    throw new InvalidOperationException(Resources.RenameVariable_NoInformationAvailableForVariable.FormatUI(originalName));
                }

                var fullName = $"{privatePrefix}{originalName}";
                for (var i = 0; i < kvp.Value.Count; i++) {
                    var reference = kvp.Value[i];
                    Debug.Assert(reference.range.start.line == reference.range.end.line);

                    var actualName = reader.ReadRange(reference.range, ast);
                    // If name does not match exactly, so we might be renaming a prefixed name
                    if (string.IsNullOrEmpty(privatePrefix)) {
                        // Not a mangled case, if names don't match, do not rename.
                        if (actualName != fullName) {
                            kvp.Value.RemoveAt(i);
                            i--;
                        }
                        continue; // All good, rename.
                    }
                    // If renaming from private name to private name, rename the non-prefixed portion
                    if (actualName.StartsWith(privatePrefix) && newName.StartsWith("__")) {
                        reference.range.start.character = reference.range.start.character + privatePrefix.Length;
                    }
                }
            }
            return refs;
        }
    }
}
