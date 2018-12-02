﻿// Python Tools for Visual Studio
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
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Python.LanguageServer.Implementation {
    public abstract class ServerBase {
        /// <summary>
        /// Doesn't do anything. Left here for legacy purpores
        /// </summary>
        public IDisposable AllowRequestCancellation(int millisecondsTimeout = -1) => EmptyDisposable.Instance;

        #region Client Requests

        public abstract Task<InitializeResult> Initialize(InitializeParams @params, CancellationToken cancellationToken);

        public virtual Task Initialized(InitializedParams @params, CancellationToken cancellationToken) => Task.CompletedTask;

        public virtual Task Shutdown() => Task.CompletedTask;

        public virtual Task Exit() => Task.CompletedTask;

        public virtual void CancelRequest() { } // Does nothing

        public abstract Task DidChangeConfiguration(DidChangeConfigurationParams @params, CancellationToken cancellationToken);

        public virtual Task DidChangeWatchedFiles(DidChangeWatchedFilesParams @params, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public virtual Task<SymbolInformation[]> WorkspaceSymbols(WorkspaceSymbolParams @params, CancellationToken cancellationToken)
            => Task.FromResult(Array.Empty<SymbolInformation>());

        public virtual Task<object> ExecuteCommand(ExecuteCommandParams @params, CancellationToken cancellationToken)
            => Task.FromResult((object)null);

        public abstract Task DidOpenTextDocument(DidOpenTextDocumentParams @params, CancellationToken cancellationToken);

        public abstract Task DidChangeTextDocument(DidChangeTextDocumentParams @params, CancellationToken cancellationToken);

        public virtual Task WillSaveTextDocument(WillSaveTextDocumentParams @params, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public virtual Task<TextEdit[]> WillSaveWaitUntilTextDocument(WillSaveTextDocumentParams @params, CancellationToken cancellationToken)
            => Task.FromResult(Array.Empty<TextEdit>());

        public virtual Task DidSaveTextDocument(DidSaveTextDocumentParams @params, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public abstract Task DidCloseTextDocument(DidCloseTextDocumentParams @params, CancellationToken cancellationToken);

        public virtual Task<CompletionList> Completion(CompletionParams @params, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public virtual Task<CompletionItem> CompletionItemResolve(CompletionItem item, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public virtual Task<Hover> Hover(TextDocumentPositionParams @params, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public virtual Task<SignatureHelp> SignatureHelp(TextDocumentPositionParams @params, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public virtual Task<Reference[]> GotoDefinition(TextDocumentPositionParams @params, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public virtual Task<Reference[]> FindReferences(ReferencesParams @params, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public virtual Task<DocumentHighlight[]> DocumentHighlight(TextDocumentPositionParams @params, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public virtual Task<SymbolInformation[]> DocumentSymbol(DocumentSymbolParams @params, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public virtual Task<DocumentSymbol[]> HierarchicalDocumentSymbol(DocumentSymbolParams @params, CancellationToken cancellationToken)
            => Task.FromResult(Array.Empty<DocumentSymbol>());

        public virtual Task<Command[]> CodeAction(CodeActionParams @params, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public virtual Task<CodeLens[]> CodeLens(TextDocumentPositionParams @params, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public virtual Task<CodeLens> CodeLensResolve(CodeLens item, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public virtual Task<DocumentLink[]> DocumentLink(DocumentLinkParams @params, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public virtual Task<DocumentLink> DocumentLinkResolve(DocumentLink item, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public virtual Task<TextEdit[]> DocumentFormatting(DocumentFormattingParams @params, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public virtual Task<TextEdit[]> DocumentRangeFormatting(DocumentRangeFormattingParams @params, CancellationToken cancellationToken) => throw new NotImplementedException();

        public virtual Task<TextEdit[]> DocumentOnTypeFormatting(DocumentOnTypeFormattingParams @params, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public virtual Task<WorkspaceEdit> Rename(RenameParams @params, CancellationToken cancellationToken)
            => throw new NotImplementedException();
        #endregion

        #region Server Requests
        public event EventHandler<ShowMessageEventArgs> OnShowMessage;

        public void ShowMessage(MessageType type, string message) 
            => OnShowMessage?.Invoke(this, new ShowMessageEventArgs { type = type, message = message });

        public event EventHandler<LogMessageEventArgs> OnLogMessage;

        public void LogMessage(MessageType type, string message) 
            => OnLogMessage?.Invoke(this, new LogMessageEventArgs { type = type, message = message });

        [Obsolete]
        public event EventHandler<TelemetryEventArgs> OnTelemetry;
        [Obsolete]
        public void Telemetry(TelemetryEventArgs e) => OnTelemetry?.Invoke(this, e);

        public event EventHandler<CommandEventArgs> OnCommand;
        public void Command(CommandEventArgs e) => OnCommand?.Invoke(this, e);

        public event EventHandler<RegisterCapabilityEventArgs> OnRegisterCapability;
        public Task RegisterCapability(RegistrationParams @params) {
            var evt = OnRegisterCapability;
            if (evt == null) {
                return Task.CompletedTask;
            }
            var tcs = new TaskCompletionSource<object>();
            var e = new RegisterCapabilityEventArgs(tcs) { @params = @params };
            evt(this, e);
            return tcs.Task;
        }


        public event EventHandler<UnregisterCapabilityEventArgs> OnUnregisterCapability;
        public Task UnregisterCapability(UnregistrationParams @params) {
            var evt = OnUnregisterCapability;
            if (evt == null) {
                return Task.CompletedTask;
            }
            var tcs = new TaskCompletionSource<object>();
            var e = new UnregisterCapabilityEventArgs(tcs) { @params = @params };
            evt(this, e);
            return tcs.Task;
        }

        public event EventHandler<ApplyWorkspaceEditEventArgs> OnApplyWorkspaceEdit;
        public Task<ApplyWorkspaceEditResponse> ApplyWorkspaceEdit(ApplyWorkspaceEditParams @params) {
            var evt = OnApplyWorkspaceEdit;
            if (evt == null) {
                return Task.FromResult((ApplyWorkspaceEditResponse)null);
            }
            var tcs = new TaskCompletionSource<ApplyWorkspaceEditResponse>();
            var e = new ApplyWorkspaceEditEventArgs(tcs) { @params = @params };
            evt(this, e);
            return tcs.Task;
        }

        public event EventHandler<PublishDiagnosticsEventArgs> OnPublishDiagnostics;
        public void PublishDiagnostics(PublishDiagnosticsEventArgs e) => OnPublishDiagnostics?.Invoke(this, e);

        #endregion

        /// <summary>
	    /// Represents a disposable that does nothing on disposal.
	    /// </summary>
	    private sealed class EmptyDisposable : IDisposable {
            /// <summary>
            /// Singleton default disposable.
            /// </summary>
            public static EmptyDisposable Instance { get; } = new EmptyDisposable();

            private EmptyDisposable() { }

            /// <summary>
            /// Does nothing.
            /// </summary>
            public void Dispose() { }
        }
    }
}
