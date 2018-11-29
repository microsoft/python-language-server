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
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer;
using Microsoft.Python.LanguageServer.Implementation;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Ast;
using TestUtilities;

namespace Microsoft.PythonTools.Analysis {
    internal static class ServerExtensions {
        public static async Task<Server> InitializeAsync(this Server server, InterpreterConfiguration configuration, Uri rootUri = null, IEnumerable<string> searchPaths = null) {
            configuration.AssertInstalled();

            server.OnLogMessage += Server_OnLogMessage;
            var properties = new InterpreterFactoryCreationOptions {
                TraceLevel = System.Diagnostics.TraceLevel.Verbose,
                DatabasePath = TestData.GetAstAnalysisCachePath(configuration.Version)
            }.ToDictionary();

            configuration.WriteToDictionary(properties);

            await server.Initialize(new InitializeParams {
                rootUri = rootUri,
                initializationOptions = new PythonInitializationOptions {
                    interpreter = new PythonInitializationOptions.Interpreter {
                        assembly = typeof(AstPythonInterpreterFactory).Assembly.Location,
                        typeName = typeof(AstPythonInterpreterFactory).FullName,
                        properties = properties
                    },
                    analysisUpdates = true,
                    searchPaths = searchPaths?.ToArray() ?? Array.Empty<string>(),
                    traceLogging = true,
                },
                capabilities = new ClientCapabilities {
                    python = new PythonClientCapabilities {
                        liveLinting = true,
                    }
                }
            }, CancellationToken.None);
            
            return server;
        }

        public static Task<IModuleAnalysis> GetAnalysisAsync(this Server server, Uri uri, int waitingTimeout = -1, int failAfter = 30000)
            => ((ProjectEntry)server.ProjectFiles.GetEntry(uri)).GetAnalysisAsync(waitingTimeout, GetCancellationToken(failAfter));

        public static Task EnqueueItemAsync(this Server server, Uri uri) 
            => server.EnqueueItemAsync((IDocument)server.ProjectFiles.GetEntry(uri));

        public static async Task EnqueueItemsAsync(this Server server, CancellationToken cancellationToken, params IDocument[] projectEntries) {
            foreach (var document in projectEntries) {
                await server.EnqueueItemAsync(document);
            }
        }

        // TODO: Replace usages of AddModuleWithContent with OpenDefaultDocumentAndGetUriAsync
        public static async Task<ProjectEntry> AddModuleWithContentAsync(this Server server, string moduleName, string relativePath, string content) {
            var entry = (ProjectEntry)server.Analyzer.AddModule(moduleName, TestData.GetTestSpecificPath(relativePath));
            entry.ResetDocument(0, content);
            await server.EnqueueItemAsync(entry);
            return entry;
        }

        public static Task<CompletionList> SendCompletion(this Server server, Uri uri, int line, int character) {
            return server.Completion(new CompletionParams {
                textDocument = new TextDocumentIdentifier {
                    uri = uri
                },
                position = new Position {
                    line = line,
                    character = character
                }
            }, CancellationToken.None);
        }

        public static Task<Hover> SendHover(this Server server, Uri uri, int line, int character) {
            return server.Hover(new TextDocumentPositionParams() {
                textDocument = new TextDocumentIdentifier {
                    uri = uri
                },
                position = new Position {
                    line = line,
                    character = character
                }
            }, CancellationToken.None);
        }

        public static Task<SignatureHelp> SendSignatureHelp(this Server server, Uri uri, int line, int character) {
            return server.SignatureHelp(new TextDocumentPositionParams {
                textDocument = uri,
                position = new Position {
                    line = line,
                    character = character
                }
            }, CancellationToken.None);
        }

        public static Task<Reference[]> SendFindReferences(this Server server, Uri uri, int line, int character, bool includeDeclaration = true) {
            return server.FindReferences(new ReferencesParams {
                textDocument = uri,
                position = new Position {
                    line = line,
                    character = character
                },
                context = new ReferenceContext {
                    includeDeclaration = includeDeclaration,
                    _includeValues = true // For compatibility with PTVS
                }
            }, CancellationToken.None);
        }

        public static async Task<Uri> OpenDefaultDocumentAndGetUriAsync(this Server server, string content) {
            var uri = TestData.GetDefaultModuleUri();
            await server.SendDidOpenTextDocument(uri, content);
            return uri;
        }

        public static async Task<Uri> OpenNextDocumentAndGetUriAsync(this Server server, string content) {
            var uri = TestData.GetNextModuleUri();
            await server.SendDidOpenTextDocument(uri, content);
            return uri;
        }

        public static async Task<Uri> OpenDocumentAndGetUriAsync(this Server server, string relativePath, string content) {
            var uri = TestData.GetTestSpecificUri(relativePath);
            await server.SendDidOpenTextDocument(uri, content);
            return uri;
        }

        public static async Task SendDidOpenTextDocument(this Server server, Uri uri, string content, string languageId = null) {
            await server.DidOpenTextDocument(new DidOpenTextDocumentParams {
                textDocument = new TextDocumentItem {
                    uri = uri,
                    text = content,
                    languageId = languageId ?? "python"
                }
            }, CancellationToken.None);
        }

        public static async Task<IModuleAnalysis> OpenDefaultDocumentAndGetAnalysisAsync(this Server server, string content, int failAfter = 30000, string languageId = null) {
            var cancellationToken = GetCancellationToken(failAfter);
            await server.SendDidOpenTextDocument(TestData.GetDefaultModuleUri(), content, languageId);
            cancellationToken.ThrowIfCancellationRequested();
            var projectEntry = (ProjectEntry) server.ProjectFiles.Single();
            return await projectEntry.GetAnalysisAsync(cancellationToken: cancellationToken);
        }

        public static Task SendDidChangeTextDocumentAsync(this Server server, Uri uri, string text) {
            return server.DidChangeTextDocument(new DidChangeTextDocumentParams {
                textDocument = new VersionedTextDocumentIdentifier {
                    uri = uri
                }, 
                contentChanges = new [] {
                    new TextDocumentContentChangedEvent {
                        text = text,
                    }, 
                }
            }, CancellationToken.None);
        }

        public static Task<TextEdit[]> SendDocumentOnTypeFormatting(this Server server, TextDocumentIdentifier textDocument, Position position, string ch) {
            return server.DocumentOnTypeFormatting(new DocumentOnTypeFormattingParams {
                textDocument = textDocument,
                position = position,
                ch = ch,
            }, CancellationToken.None);
        }

        public static Task SendDidChangeConfiguration(this Server server, ServerSettings.PythonCompletionOptions pythonCompletionOptions, int failAfter = 30000) {
            var currentSettings = server.Settings;
            var settings = new LanguageServerSettings();

            settings.completion.showAdvancedMembers = pythonCompletionOptions.showAdvancedMembers;
            settings.completion.addBrackets = pythonCompletionOptions.addBrackets;

            settings.analysis.openFilesOnly = currentSettings.analysis.openFilesOnly;
            if (currentSettings is LanguageServerSettings languageServerSettings) {
                settings.diagnosticPublishDelay = languageServerSettings.diagnosticPublishDelay;
                settings.symbolsHierarchyDepthLimit = languageServerSettings.symbolsHierarchyDepthLimit;
            }

            var errors = currentSettings.analysis.errors;
            var warnings = currentSettings.analysis.warnings;
            var information = currentSettings.analysis.information;
            var disabled = currentSettings.analysis.disabled;
            settings.analysis.SetErrorSeverityOptions(errors, warnings, information, disabled);

            return server.SendDidChangeConfiguration(settings, failAfter);
        }

        public static Task SendDidChangeConfiguration(this Server server, ServerSettings settings, int failAfter = 30000) 
            => server.DidChangeConfiguration(new DidChangeConfigurationParams { settings = settings }, GetCancellationToken(failAfter));

        public static async Task<IModuleAnalysis> ChangeDefaultDocumentAndGetAnalysisAsync(this Server server, string text, int failAfter = 30000) {
            var projectEntry = (ProjectEntry) server.ProjectFiles.Single();
            await server.SendDidChangeTextDocumentAsync(projectEntry.DocumentUri, text);
            return await projectEntry.GetAnalysisAsync(cancellationToken: GetCancellationToken(failAfter));
        }

        public static string[] GetBuiltinTypeMemberNames(this Server server, BuiltinTypeId typeId) 
            => server.Analyzer.Types[typeId].GetMemberNames((IModuleContext)server.Analyzer.Interpreter).ToArray();

        private static void Server_OnLogMessage(object sender, LogMessageEventArgs e) {
            switch (e.type) {
                case MessageType.Error: Trace.TraceError($"[{TestEnvironmentImpl.Elapsed()}]: {e.message}"); break;
                case MessageType.Warning: Trace.TraceWarning($"[{TestEnvironmentImpl.Elapsed()}]: {e.message}"); break;
                case MessageType.Info: Trace.TraceInformation($"[{TestEnvironmentImpl.Elapsed()}]: {e.message}"); break;
                case MessageType.Log: Trace.TraceInformation($"[{TestEnvironmentImpl.Elapsed()}] LOG: {e.message}"); break;
            }
        }

        private static CancellationToken GetCancellationToken(int failAfter = 30000) 
            => Debugger.IsAttached ? CancellationToken.None : new CancellationTokenSource(failAfter).Token;
    }
}
