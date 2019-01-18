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

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Python.Core.Text;

namespace Microsoft.Python.LanguageServer.Protocol {
    [Serializable]
    public struct InitializeParams {
        public int? processId;
        public string rootPath;
        public Uri rootUri;
        public PythonInitializationOptions initializationOptions;
        public ClientCapabilities capabilities;
        public TraceLevel trace;
    }

    [Serializable]
    public class LanguageServerException : Exception {
        public const int UnknownDocument = 1;
        public const int UnsupportedDocumentType = 2;
        public const int MismatchedVersion = 3;
        public const int UnknownExtension = 4;

        public int Code => (int)Data["Code"];

        public sealed override System.Collections.IDictionary Data => base.Data;

        public LanguageServerException(int code, string message) : base(message) {
            Data["Code"] = code;
        }

        public LanguageServerException(int code, string message, Exception innerException) : base(message, innerException) {
            Data["Code"] = code;
        }
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

        // Defaults to true, but can be set to false to suppress analysis
        public bool? _enqueueForAnalysis;
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

    public sealed class class PublishDiagnosticsEventArgs : EventArgs {
        public Uri uri { get; set; }
        public IReadOnlyList<Diagnostic> diagnostics { get; set; }

        /// <summary>
        /// The version the ranges in the diagnostics apply to.
        /// </summary>
        public int? _version { get; set; }
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
        public Diagnostic[] diagnostics;

        /// <summary>
        /// The intended version that diagnostic locations apply to. The request may
        /// fail if the server cannot map correctly.
        /// </summary>
        public int? _version;
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
    public class PythonAnalysisExtensionParams {
        public string assembly;
        public string typeName;
        public Dictionary<string, object> properties;
    }

    [Serializable]
    public class ExtensionCommandParams {
        public string extensionName;
        public string command;
        public Dictionary<string, object> properties;
    }

    [Serializable]
    public class ExtensionCommandResult {
        public IReadOnlyDictionary<string, object> properties;
    }
}
