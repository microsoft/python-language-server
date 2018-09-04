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

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Python.LanguageServer {
    /// <summary>
    /// Implements part of the language protocol relevant to the extensibility.
    /// <see cref="IPythonLanguageServerExtension"/>
    /// </summary>
    /// <remarks>
    /// https://microsoft.github.io/language-server-protocol/specification
    /// </remarks>
    public interface IPythonLanguageServerProtocol {
        Task<SymbolInformation[]> WorkspaceSymbols(WorkspaceSymbolParams @params, CancellationToken token);
        Task<object> ExecuteCommand(ExecuteCommandParams @params, CancellationToken token);
        Task<CompletionList> Completion(CompletionParams @params, CancellationToken token);
        Task<CompletionItem> CompletionItemResolve(CompletionItem item, CancellationToken token);
        Task<Hover> Hover(TextDocumentPositionParams @params, CancellationToken token);
        Task<SignatureHelp> SignatureHelp(TextDocumentPositionParams @params, CancellationToken token);
        Task<Reference[]> GotoDefinition(TextDocumentPositionParams @params, CancellationToken token);
        Task<Reference[]> FindReferences(ReferencesParams @params, CancellationToken token);
        Task<DocumentHighlight[]> DocumentHighlight(TextDocumentPositionParams @params, CancellationToken token);
        Task<SymbolInformation[]> DocumentSymbol(DocumentSymbolParams @params, CancellationToken token);
        Task<DocumentSymbol[]> HierarchicalDocumentSymbol(DocumentSymbolParams @params, CancellationToken cancellationToken);
        Task<Command[]> CodeAction(CodeActionParams @params, CancellationToken token);
        Task<CodeLens[]> CodeLens(TextDocumentPositionParams @params, CancellationToken token);
        Task<CodeLens> CodeLensResolve(CodeLens item, CancellationToken token);
        Task<DocumentLink[]> DocumentLink(DocumentLinkParams @params, CancellationToken token);
        Task<DocumentLink> DocumentLinkResolve(DocumentLink item, CancellationToken token);
        Task<TextEdit[]> DocumentFormatting(DocumentFormattingParams @params, CancellationToken token);
        Task<TextEdit[]> DocumentRangeFormatting(DocumentRangeFormattingParams @params, CancellationToken token);
        Task<TextEdit[]> DocumentOnTypeFormatting(DocumentOnTypeFormattingParams @params, CancellationToken token);
        Task<WorkspaceEdit> Rename(RenameParams @params, CancellationToken cancellationToken);
    }
}
