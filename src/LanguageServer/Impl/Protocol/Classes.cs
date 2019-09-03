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
using Microsoft.Python.Core.Text;
using Newtonsoft.Json;

namespace Microsoft.Python.LanguageServer.Protocol {
    [Serializable]
    public sealed class ResponseError {
        public int code;
        public string message;
    }

    [Serializable]
    public sealed class Location {
        public Uri uri;
        public Range range;
    }

    [Serializable]
    public sealed class Command {
        /// <summary>
        /// Title of the command, like `save`.
        /// </summary>
        public string title;

        /// <summary>
        /// The identifier of the actual command handler.
        /// </summary>
        public string command;

        /// <summary>
        /// Arguments that the command handler should be invoked with.
        /// </summary>
        public object[] arguments;
    }

    [Serializable]
    public sealed class TextEdit {
        /// <summary>
        /// The range of the text document to be manipulated. To insert
        /// text into a document create a range where start === end.
        /// </summary>
        public Range range;

        /// <summary>
        /// The string to be inserted. For delete operations use an
        /// empty string.
        /// </summary>
        public string newText;
    }

    [Serializable]
    public sealed class TextDocumentEdit {
        public VersionedTextDocumentIdentifier textDocument;
        public TextEdit[] edits;
    }

    [Serializable]
    public sealed class WorkspaceEdit {
        public Dictionary<Uri, TextEdit[]> changes;
        public TextDocumentEdit[] documentChanges;
    }

    [Serializable]
    public sealed class TextDocumentIdentifier {
        public Uri uri;

        public static implicit operator TextDocumentIdentifier(Uri uri) => new TextDocumentIdentifier { uri = uri };
    }

    [Serializable]
    public sealed class TextDocumentItem {
        public Uri uri;
        public string languageId;
        public int version;
        public string text;
    }

    [Serializable]
    public sealed class VersionedTextDocumentIdentifier {
        public Uri uri;
        public int? version;
    }

    [Serializable]
    public sealed class DocumentFilter {
        /// <summary>
        /// A language id, like `typescript`.
        /// </summary>
        public string language;

        /// <summary>
        /// A Uri [scheme](#Uri.scheme), like `file` or `untitled`.
        /// </summary>
        public string scheme;

        /// <summary>
        /// A glob pattern, like `*.{ts,js}`.
        /// </summary>
        public string pattern;
    }

    /// <summary>
    /// Required layout for the initializationOptions member of initializeParams
    /// </summary>
    [Serializable]
    public sealed class PythonInitializationOptions {
        [Serializable]
        public struct Interpreter {
            public sealed class InterpreterProperties {
                public string Version;
                public string InterpreterPath;
                public string DatabasePath;
            }
            public InterpreterProperties properties;
        }
        public Interpreter interpreter;

        /// <summary>
        /// Paths to search when attempting to resolve module imports.
        /// </summary>
        public string[] searchPaths = Array.Empty<string>();

        /// <summary>
        /// Paths to search for module stubs.
        /// </summary>
        public string[] typeStubSearchPaths = Array.Empty<string>();

        /// <summary>
        /// Glob pattern of files and folders to exclude from loading
        /// into the Python analysis engine.
        /// </summary>
        public string[] excludeFiles = Array.Empty<string>();

        /// <summary>
        /// Glob pattern of files and folders under the root folder that
        /// should be loaded into the Python analysis engine.
        /// </summary>
        public string[] includeFiles = Array.Empty<string>();

        /// <summary>
        /// Path to a writable folder where analyzer can cache its data.
        /// </summary>
        public string cacheFolderPath;
    }

    [Serializable]
    public sealed class WorkspaceClientCapabilities {
        public bool applyEdit;

        public struct WorkspaceEditCapabilities { public bool documentChanges; }
        public WorkspaceEditCapabilities? documentChanges;

        public struct DidConfigurationChangeCapabilities { public bool dynamicRegistration; }
        public DidConfigurationChangeCapabilities? didConfigurationChange;

        public struct DidChangeWatchedFilesCapabilities { public bool dynamicRegistration; }
        public DidChangeWatchedFilesCapabilities? didChangeWatchedFiles;

        [Serializable]
        public struct SymbolCapabilities {
            public bool dynamicRegistration;

            [Serializable]
            public struct SymbolKindCapabilities {
                /// <summary>
                /// The symbol kind values the client supports. When this
                /// property exists the client also guarantees that it will
                /// handle values outside its set gracefully and falls back
                /// to a default value when unknown.
                /// 
                /// If this property is not present the client only supports
                /// the symbol kinds from `File` to `Array` as defined in
                /// the initial version of the protocol.
                /// </summary>
                public SymbolKind[] valueSet;
            }
            public SymbolKindCapabilities? symbolKind;
        }

        public SymbolCapabilities? symbol;

        public struct ExecuteCommandCapabilities { public bool dynamicRegistration; }
        public ExecuteCommandCapabilities? executeCommand;
    }

    [Serializable]
    public class TextDocumentClientCapabilities {
        [Serializable]
        public struct SynchronizationCapabilities {
            public bool dynamicRegistration;
            public bool willSave;
            /// <summary>
            /// The client supports sending a will save request and
            /// waits for a response providing text edits which will
            /// be applied to the document before it is saved.
            /// </summary>
            public bool willSaveWaitUntil;
            public bool didSave;
        }
        public SynchronizationCapabilities? synchronization;

        [Serializable]
        public sealed class CompletionCapabilities {
            public bool dynamicRegistration;

            [Serializable]
            public sealed class CompletionItemCapabilities {
                /// <summary>
                /// Client supports snippets as insert text.
                /// 
                /// A snippet can define tab stops and placeholders with `$1`, `$2`
                /// and `${3:foo}`. `$0` defines the final tab stop, it defaults to
                /// the end of the snippet. Placeholders with equal identifiers are linked,
                /// that is typing in one will update others too.
                /// </summary>
                public bool snippetSupport;

                public bool commitCharactersSupport;

                public string[] documentationFormat;
            }
            public CompletionItemCapabilities completionItem;

            [Serializable]
            public sealed class CompletionItemKindCapabilities {
                /// <summary>
                /// The completion item kind values the client supports. When this
                /// property exists the client also guarantees that it will
                /// handle values outside its set gracefully and falls back
                /// to a default value when unknown.
                /// 
                /// If this property is not present the client only supports
                /// the completion items kinds from `Text` to `Reference` as defined in
                /// the initial version of the protocol.
                /// </summary>
                public SymbolKind[] valueSet;
            }
            public CompletionItemKindCapabilities completionItemKind;

            /// <summary>
            /// The client supports to send additional context information for a
            /// `textDocument/completion` request.
            /// </summary>
            public bool contextSupport;
        }
        public CompletionCapabilities completion;

        [Serializable]
        public sealed class HoverCapabilities {
            public bool dynamicRegistration;
            /// <summary>
            /// Client supports the follow content formats for the content
            /// property.The order describes the preferred format of the client.
            /// </summary>
            public string[] contentFormat;
        }
        public HoverCapabilities hover;

        [Serializable]
        public sealed class SignatureHelpCapabilities {
            public bool dynamicRegistration;

            public struct SignatureInformationCapabilities {
                /// <summary>
                ///  Client supports the follow content formats for the documentation
                /// property.The order describes the preferred format of the client.
                /// </summary>
                public string[] documentationFormat;

                /// <summary>
                /// Client capabilities specific to parameter information.
                /// </summary>
                public struct ParameterInformationCapabilities {
                    /// <summary>
                    ///  The client supports processing label offsets instead of a simple label string
                    /// </summary>
                    public bool? labelOffsetSupport;
                }
                public ParameterInformationCapabilities? parameterInformation;
            }
            public SignatureInformationCapabilities? signatureInformation;
        }
        public SignatureHelpCapabilities signatureHelp;

        [Serializable]
        public sealed class ReferencesCapabilities { public bool dynamicRegistration; }
        public ReferencesCapabilities references;

        [Serializable]
        public sealed class DocumentHighlightCapabilities { public bool dynamicRegistration; }
        public DocumentHighlightCapabilities documentHighlight;

        [Serializable]
        public sealed class DocumentSymbolCapabilities {
            public bool dynamicRegistration;
            public sealed class SymbolKindCapabilities {
                /// <summary>
                /// The symbol kind values the client supports. When this
                /// property exists the client also guarantees that it will
                /// handle values outside its set gracefully and falls back
                /// to a default value when unknown.
                /// 
                /// If this property is not present the client only supports
                /// the symbol kinds from `File` to `Array` as defined in
                /// the initial version of the protocol.
                /// </summary>
                public SymbolKind[] valueSet;
            }
            public SymbolKindCapabilities symbolKind;

            /// <summary>
            /// The client support hierarchical document symbols.
            /// </summary>
            public bool? hierarchicalDocumentSymbolSupport;
        }
        public DocumentSymbolCapabilities documentSymbol;

        [Serializable]
        public sealed class FormattingCapabilities { public bool dynamicRegistration; }
        public FormattingCapabilities formatting;

        [Serializable]
        public sealed class RangeFormattingCapabilities { public bool dynamicRegistration; }
        public RangeFormattingCapabilities rangeFormatting;

        [Serializable]
        public sealed class OnTypeFormattingCapabilities { public bool dynamicRegistration; }
        public OnTypeFormattingCapabilities onTypeFormatting;

        public sealed class DefinitionCapabilities { public bool dynamicRegistration; }
        public DefinitionCapabilities definition;

        [Serializable]
        public sealed class CodeActionCapabilities { public bool dynamicRegistration; }
        public CodeActionCapabilities codeAction;

        [Serializable]
        public sealed class CodeLensCapabilities { public bool dynamicRegistration; }
        public CodeLensCapabilities codeLens;

        [Serializable]
        public sealed class DocumentLinkCapabilities { public bool dynamicRegistration; }
        public DocumentLinkCapabilities documentLink;

        [Serializable]
        public sealed class RenameCapabilities { public bool dynamicRegistration; }
        public RenameCapabilities rename;
    }

    [Serializable]
    public sealed class ClientCapabilities {
        public WorkspaceClientCapabilities workspace;
        public TextDocumentClientCapabilities textDocument;
    }

    [Serializable]
    public sealed class CompletionOptions {
        /// <summary>
        /// The server provides support to resolve additional
        /// information for a completion item.
        /// </summary>
        public bool resolveProvider;
        /// <summary>
        /// The characters that trigger completion automatically.
        /// </summary>
        public string[] triggerCharacters;
    }

    [Serializable]
    public sealed class SignatureHelpOptions {
        /// <summary>
        /// The characters that trigger signature help
        /// automatically.
        /// </summary>
        public string[] triggerCharacters;
    }

    [Serializable]
    public sealed class CodeLensOptions {
        public bool resolveProvider;
    }

    [Serializable]
    public sealed class DocumentOnTypeFormattingOptions {
        public string firstTriggerCharacter;
        public string[] moreTriggerCharacter;
    }

    [Serializable]
    public sealed class DocumentLinkOptions {
        public bool resolveProvider;
    }

    [Serializable]
    public sealed class ExecuteCommandOptions {
        public string[] commands;
    }

    [Serializable]
    public sealed class SaveOptions {
        public bool includeText;
    }

    [Serializable]
    public sealed class TextDocumentSyncOptions {
        /// <summary>
        /// Open and close notifications are sent to the server.
        /// </summary>
        public bool openClose;
        public TextDocumentSyncKind change;
        public bool willSave;
        public bool willSaveWaitUntil;
        public SaveOptions save;
    }

    [Serializable]
    public sealed class ServerCapabilities {
        public TextDocumentSyncOptions textDocumentSync;
        public bool hoverProvider;
        public CompletionOptions completionProvider;
        public SignatureHelpOptions signatureHelpProvider;
        public bool definitionProvider;
        public bool referencesProvider;
        public bool documentHighlightProvider;
        public bool documentSymbolProvider;
        public bool workspaceSymbolProvider;
        public bool codeActionProvider;
        public CodeLensOptions codeLensProvider;
        public bool documentFormattingProvider;
        public bool documentRangeFormattingProvider;
        public DocumentOnTypeFormattingOptions documentOnTypeFormattingProvider;
        public bool renameProvider;
        public DocumentLinkOptions documentLinkProvider;
        public bool declarationProvider; // 3.14.0+
        public ExecuteCommandOptions executeCommandProvider;
        public object experimental;
    }

    [Serializable]
    public sealed class MessageActionItem {
        public string title;
    }

    [Serializable]
    public sealed class Registration {
        public string id;
        public string method;
        public IRegistrationOptions registerOptions;
    }

    public interface IRegistrationOptions { }

    [Serializable]
    public sealed class TextDocumentRegistrationOptions : IRegistrationOptions {
        public DocumentFilter documentSelector;
    }

    [Serializable]
    public struct Unregistration {
        public string id;
        public string method;
    }

    [Serializable]
    public sealed class FileEvent {
        public Uri uri;
        public FileChangeType type;
    }

    [Serializable]
    public sealed class DidChangeWatchedFilesRegistrationOptions : IRegistrationOptions {
        public FileSystemWatcher[] watchers;
    }

    [Serializable]
    public sealed class FileSystemWatcher {
        public string globPattern;
        public WatchKind? type;
    }

    [Serializable]
    public sealed class ExecuteCommandRegistrationOptions : IRegistrationOptions {
        public string[] commands;
    }

    [Serializable]
    public sealed class TextDocumentContentChangedEvent {
        public Range? range;
        public int? rangeLength;
        public string text;
    }

    public sealed class TextDocumentChangeRegistrationOptions : IRegistrationOptions {
        public DocumentFilter documentSelector;
        public TextDocumentSyncKind syncKind;
    }

    [Serializable]
    public struct TextDocumentSaveRegistrationOptions : IRegistrationOptions {
        public DocumentFilter documentSelector;
        public bool includeText;
    }

    [Serializable]
    public class CompletionList {
        /// <summary>
        /// This list is not complete. Further typing should result in recomputing
        /// this list.
        /// </summary>
        public bool isIncomplete;
        public CompletionItem[] items;

        /// <summary>
        /// The range that should be replaced when committing a completion from this
        /// list. Where <c>textEdit</c> is set on a completion, prefer that.
        /// </summary>
        public Range? _applicableSpan;
    }

    [Serializable]
    [DebuggerDisplay("{label}")]
    public class CompletionItem {
        public string label;
        public CompletionItemKind kind;
        public string detail;
        public MarkupContent documentation;
        public string sortText;
        public string filterText;
        public bool? preselect; // VS Code 1.25+
        public string insertText;
        public InsertTextFormat insertTextFormat;
        public TextEdit textEdit;
        public TextEdit[] additionalTextEdits;
        public string[] commitCharacters;
        public Command command;
        public object data;
    }

    // Not in LSP spec
    [Serializable]
    public struct CompletionItemValue {
        public string description;
        public string documentation;
        public Reference[] references;
    }

    [Serializable]
    public sealed class CompletionRegistrationOptions : IRegistrationOptions {
        public DocumentFilter documentSelector;
        public string[] triggerCharacters;
        public bool resolveProvider;
    }

    [Serializable]
    public sealed class Hover {
        public MarkupContent contents;
        public Range? range;

        /// <summary>
        /// The document version that range applies to.
        /// </summary>
        public int? _version;
    }

    [Serializable]
    public sealed class SignatureHelp {
        public SignatureInformation[] signatures;
        public int activeSignature;
        public int activeParameter;
    }

    [Serializable]
    public sealed class SignatureInformation {
        public string label;
        public MarkupContent documentation;
        public ParameterInformation[] parameters;
    }

    [Serializable]
    public sealed class ParameterInformation {
        /// <summary>
        /// The label of this signature.
        /// </summary>
        /// <remarks>
        /// LSP before 3.14: string label.
        /// LSP 3.14.0+: (int, int) range.
        /// Either a string or inclusive start and exclusive end offsets within its containing
        /// [signature label] (#SignatureInformation.label). *Note*: A label of type string must be
        /// a substring of its containing signature information's [label](#SignatureInformation.label).
        /// </remarks>
        public object label;
        public MarkupContent documentation;
    }

    /// <summary>
    /// Used instead of Position when returning references so we can include
    /// the kind.
    /// </summary>
    [Serializable]
    public sealed class Reference {
        public Uri uri;
        public Range range;
    }

    [Serializable]
    public sealed class DocumentHighlight {
        public Range range;
        public DocumentHighlightKind kind;

        /// <summary>
        /// The document version that range applies to
        /// </summary>
        public int? _version;
    }

    [Serializable]
    public sealed class DocumentSymbol {
        /// <summary>
        /// The name of this symbol.
        /// </summary>
        public string name;

        /// <summary>
        /// More detail for this symbol, e.g the signature of a function. If not provided the
        /// name is used.
        /// </summary>
        public string detail;

        /// <summary>
        /// The kind of this symbol.
        /// </summary>
        public SymbolKind kind;

        /// <summary>
        /// Indicates if this symbol is deprecated.
        /// </summary>
        public bool deprecated;

        /// <summary>
        /// The range enclosing this symbol not including leading/trailing whitespace but everything else
        /// like comments.This information is typically used to determine if the clients cursor is
        /// inside the symbol to reveal in the symbol in the UI.
        /// </summary>
        public Range range;

        /// <summary>
        /// The range that should be selected and revealed when this symbol is being picked, 
        /// e.g the name of a function. Must be contained by the `range`.
        /// </summary>
        public Range selectionRange;

        /// <summary>
        /// Children of this symbol, e.g. properties of a class.
        /// </summary>
        public DocumentSymbol[] children;
    }

    [Serializable]
    public sealed class SymbolInformation {
        public string name;
        public SymbolKind kind;
        public Location location;
        /// <summary>
        /// The name of the symbol containing this symbol. This information is for
        /// user interface purposes (e.g.to render a qualifier in the user interface
        /// if necessary). It can't be used to re-infer a hierarchy for the document
        /// symbols.
        /// </summary>
        public string containerName;
    }

    [Serializable]
    public sealed class CodeLens {
        public Range range;
        public Command command;
        public object data;
    }

    [Serializable]
    public sealed class DocumentLink {
        public Range range;
        public Uri target;
    }

    [Serializable]
    public struct DocumentLinkRegistrationOptions : IRegistrationOptions {
        public DocumentFilter documentSelector;
        public bool resolveProvider;
    }

    [Serializable]
    public sealed class FormattingOptions {
        public int tabSize;
        public bool insertSpaces;

    }

    [Serializable]
    public sealed class DocumentOnTypeFormattingRegistrationOptions : IRegistrationOptions {
        public DocumentFilter documentSelector;
        public string firstTriggerCharacter;
        public string[] moreTriggerCharacters;
    }

    [JsonObject]
    public sealed class PublishDiagnosticsParams {
        [JsonProperty]
        public Uri uri;
        [JsonProperty]
        public Diagnostic[] diagnostics;
    }
}
