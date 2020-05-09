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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.LanguageServer.Completion;
using Microsoft.Python.LanguageServer.Documents;
using Microsoft.Python.LanguageServer.Extensibility;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.LanguageServer.Sources;

namespace Microsoft.Python.LanguageServer.Implementation {
    public sealed partial class Server {
        private const int CompletionAnalysisTimeout = 200;

        private CompletionSource _completionSource;
        private HoverSource _hoverSource;
        private SignatureSource _signatureSource;

        public async Task<CompletionList> Completion(CompletionParams @params, CancellationToken cancellationToken) {
            var uri = @params.textDocument.uri;
            _log?.Log(TraceEventType.Verbose, $"Completions in {uri} at {@params.position}");

            var res = new CompletionList();
            var analysis = await Document.GetAnalysisAsync(uri, Services, CompletionAnalysisTimeout, cancellationToken);
            if (analysis != null) {
                var result = _completionSource.GetCompletions(analysis, @params.position);
                res.items = result?.Completions?.ToArray() ?? Array.Empty<CompletionItem>();

                await InvokeExtensionsAsync(async (ext, token)
                    => {
                        switch (ext) {
                            case ICompletionExtension2 e:
                                await e.HandleCompletionAsync(analysis, @params.position, res, cancellationToken);
                                break;
                            case ICompletionExtension e:
                                await e.HandleCompletionAsync(analysis, @params.position, res.items.OfType<CompletionItemEx>().ToArray(), cancellationToken);
                                break;
                            default:
                                // ext is not a completion extension, ignore it.
                                break;
                        }
                    }, cancellationToken);
            }

            return res;
        }

        public async Task<Hover> Hover(TextDocumentPositionParams @params, CancellationToken cancellationToken) {
            var uri = @params.textDocument.uri;
            _log?.Log(TraceEventType.Verbose, $"Hover in {uri} at {@params.position}");

            var analysis = await Document.GetAnalysisAsync(uri, Services, CompletionAnalysisTimeout, cancellationToken);
            if (analysis != null) {
                return _hoverSource.GetHover(analysis, @params.position);
            }
            return null;
        }

        public async Task<SignatureHelp> SignatureHelp(TextDocumentPositionParams @params, CancellationToken cancellationToken) {
            var uri = @params.textDocument.uri;
            _log?.Log(TraceEventType.Verbose, $"Signatures in {uri} at {@params.position}");

            var analysis = await Document.GetAnalysisAsync(uri, Services, CompletionAnalysisTimeout, cancellationToken);
            if (analysis != null) {
                return _signatureSource.GetSignature(analysis, @params.position);
            }
            return null;
        }

        public async Task<Reference[]> GotoDefinition(TextDocumentPositionParams @params, CancellationToken cancellationToken) {
            var uri = @params.textDocument.uri;
            _log?.Log(TraceEventType.Verbose, $"Goto Definition in {uri} at {@params.position}");

            var analysis = await Document.GetAnalysisAsync(uri, Services, CompletionAnalysisTimeout, cancellationToken);
            var ds = new DefinitionSource(Services);
            var reference = ds.FindDefinition(analysis, @params.position, out _);
            return reference != null && ds.CanNavigateToModule(reference.uri) 
                ? new[] { reference } 
                : Array.Empty<Reference>();
        }

        public async Task<Location> GotoDeclaration(TextDocumentPositionParams @params, CancellationToken cancellationToken) {
            var uri = @params.textDocument.uri;
            _log?.Log(TraceEventType.Verbose, $"Goto Declaration in {uri} at {@params.position}");

            var analysis = await Document.GetAnalysisAsync(uri, Services, CompletionAnalysisTimeout, cancellationToken);
            var ds = new DeclarationSource(Services);
            var reference = ds.FindDefinition(analysis, @params.position, out _);
            return reference != null && ds.CanNavigateToModule(reference.uri) 
                ? new Location { uri = reference.uri, range = reference.range } 
                : null;
        }

        public Task<Reference[]> FindReferences(ReferencesParams @params, CancellationToken cancellationToken) {
            var uri = @params.textDocument.uri;
            _log?.Log(TraceEventType.Verbose, $"References in {uri} at {@params.position}");
            return new ReferenceSource(Services).FindAllReferencesAsync(uri, @params.position, ReferenceSearchOptions.All, cancellationToken);
        }

        public Task<DocumentHighlight[]> DocumentHighlight(ReferencesParams @params, CancellationToken cancellationToken) {
            var uri = @params.textDocument.uri;
            _log?.Log(TraceEventType.Verbose, $"Document highlight in {uri} at {@params.position}");
            return new DocumentHighlightSource(Services).DocumentHighlightAsync(uri, @params.position, cancellationToken);
        }

        public Task<WorkspaceEdit> Rename(RenameParams @params, CancellationToken cancellationToken) {
            var uri = @params.textDocument.uri;
            _log?.Log(TraceEventType.Verbose, $"Rename in {uri} at {@params.position}");
            return new RenameSource(Services).RenameAsync(uri, @params.position, @params.newName, cancellationToken);
        }

        public async Task<CodeAction[]> CodeAction(CodeActionParams @params, CancellationToken cancellationToken) {
            var uri = @params.textDocument.uri;
            _log?.Log(TraceEventType.Verbose, $"Code Action in {uri} at {@params.range}");

            var codeActions = new List<CodeAction>();
            var analysis = await Document.GetAnalysisAsync(uri, Services, CompletionAnalysisTimeout, cancellationToken);

            if (AskedFor(@params, CodeActionKind.Refactor)) {
                codeActions.AddRange(await new RefactoringCodeActionSource(Services).GetCodeActionsAsync(analysis, _codeActionSettings, @params.range, cancellationToken));
            }

            if (@params.context.diagnostics?.Length > 0 && AskedFor(@params, CodeActionKind.QuickFix)) {
                codeActions.AddRange(await new QuickFixCodeActionSource(Services).GetCodeActionsAsync(analysis, _codeActionSettings, @params.context.diagnostics, cancellationToken));
            }

            return codeActions.ToArray();

            bool AskedFor(CodeActionParams p, string codeActionKind) {
                return p.context.only == null || p.context.only.Any(s => s.StartsWith(codeActionKind));
            }
        }
    }
}
