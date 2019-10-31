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
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Python.Core.Text;
using Range = Microsoft.Python.Core.Text.Range;

namespace Microsoft.Python.LanguageServer.Protocol {
    [Serializable]
    public sealed class InitializeParams {
        public int? processId;
        public string rootPath;
        public Uri rootUri;
        public PythonInitializationOptions initializationOptions;
        public ClientCapabilities capabilities;
        public TraceLevel trace;
    }

    [Serializable]
    public struct InitializeResult {
        public ServerCapabilities capabilities;
    }

    [Serializable]
    public struct InitializedParams { }

    public sealed class ShowMessageEventArgs : EventArgs {
        public MessageType type { get; set; }
        public string message { get; set; }
    }

    [Serializable]
    public class ShowMessageRequestParams {
        public MessageType type;
        public string message;
        public MessageActionItem[] actions;
    }

    public sealed class CommandEventArgs : EventArgs {
        public string command;
        public object[] arguments;
    }

    [Serializable]
    public sealed class RegistrationParams {
        public Registration[] registrations;
    }

    [ComVisible(false)]
    public sealed class RegisterCapabilityEventArgs : CallbackEventArgs<RegistrationParams> {
        internal RegisterCapabilityEventArgs(TaskCompletionSource<object> task) : base(task) { }
    }

    [Serializable]
    public sealed class UnregistrationParams {
        public Unregistration[] unregistrations;
    }

    [ComVisible(false)]
    public sealed class UnregisterCapabilityEventArgs : CallbackEventArgs<UnregistrationParams> {
        internal UnregisterCapabilityEventArgs(TaskCompletionSource<object> task) : base(task) { }
    }

    [Serializable]
    public sealed class DidChangeConfigurationParams {
        public object settings;
    }

    [Serializable]
    public sealed class DidChangeWatchedFilesParams {
        public FileEvent[] changes;
    }

    [Serializable]
    public sealed class WorkspaceSymbolParams {
        public string query;
    }

    [Serializable]
    public sealed class ExecuteCommandParams {
        public string command;
        public object[] arguments;
    }

    [Serializable]
    public sealed class ApplyWorkspaceEditParams {
        /// <summary>
        /// An optional label of the workspace edit.This label is
        /// presented in the user interface for example on an undo
        /// stack to undo the workspace edit.
        /// </summary>
        public string label;
        public WorkspaceEdit edit;
    }

    [Serializable]
    public sealed class ApplyWorkspaceEditResponse {
        public bool applied;
    }

    [ComVisible(false)]
    public sealed class ApplyWorkspaceEditEventArgs : CallbackEventArgs<ApplyWorkspaceEditParams, ApplyWorkspaceEditResponse> {
        internal ApplyWorkspaceEditEventArgs(TaskCompletionSource<ApplyWorkspaceEditResponse> task) : base(task) { }
    }

    [Serializable]
    public sealed class DidOpenTextDocumentParams {
        public TextDocumentItem textDocument;
    }

    [Serializable]
    public sealed class DidChangeTextDocumentParams {
        public VersionedTextDocumentIdentifier textDocument;
        public TextDocumentContentChangedEvent[] contentChanges;
    }

    [Serializable]
    public sealed class WillSaveTextDocumentParams {
        public TextDocumentIdentifier textDocument;
        public TextDocumentSaveReason reason;
    }

    [Serializable]
    public sealed class DidSaveTextDocumentParams {
        public TextDocumentIdentifier textDocument;
        public string content;
    }

    [Serializable]
    public sealed class DidCloseTextDocumentParams {
        public TextDocumentIdentifier textDocument;
    }

    [Serializable]
    public sealed class TextDocumentPositionParams {
        public TextDocumentIdentifier textDocument;
        public Position position;
    }

    [Serializable]
    public sealed class CompletionParams {
        public TextDocumentIdentifier textDocument;
        public Position position;
        public CompletionContext context;
    }

    [Serializable]
    public sealed class CompletionContext {
        public CompletionTriggerKind triggerKind;
        public string triggerCharacter;
    }

    [Serializable]
    public sealed class ReferencesParams {
        public TextDocumentIdentifier textDocument;
        public Position position;
        public ReferenceContext context;
    }

    public sealed class ReferenceContext {
        public bool includeDeclaration;
    }

    [Serializable]
    public sealed class DocumentSymbolParams {
        public TextDocumentIdentifier textDocument;
    }

    [Serializable]
    public struct CodeActionParams {
        public TextDocumentIdentifier textDocument;
        public Range range;
        public CodeActionContext context;
    }

    [Serializable]
    public sealed class CodeActionContext {
        // 
	    // An array of diagnostics.
	    // 
        public Diagnostic[] diagnostics;
        // 
	    // Requested kind of actions to return.
	    // 
	    // Actions not of this kind are filtered out by the client before being shown. So servers
	    // can omit computing them.
	    // 
        public string[] only;
    }

    [Serializable]
    public sealed class DocumentLinkParams {
        public TextDocumentIdentifier textDocument;
    }

    [Serializable]
    public sealed class DocumentFormattingParams {
        public TextDocumentIdentifier textDocument;
        public FormattingOptions options;
    }

    [Serializable]
    public sealed class DocumentRangeFormattingParams {
        public TextDocumentIdentifier textDocument;
        public Range range;
        public FormattingOptions options;
    }

    [Serializable]
    public sealed class DocumentOnTypeFormattingParams {
        public TextDocumentIdentifier textDocument;
        public Position position;
        public string ch;
        public FormattingOptions options;
    }

    [Serializable]
    public sealed class RenameParams {
        public TextDocumentIdentifier textDocument;
        public Position position;
        public string newName;
    }

    [Serializable]
    public sealed class LogMessageParams {
        public MessageType type;
        public string message;
    }

    [Serializable]
    public sealed class ConfigurationItem {
        public Uri scopeUri;
        public string section;
    }

    [Serializable]
    public sealed class ConfigurationParams {
        public ConfigurationItem[] items;
    }
}
