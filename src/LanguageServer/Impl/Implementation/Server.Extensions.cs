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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

#pragma warning disable CS0618 // Type or member is obsolete

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Core;
using Microsoft.Python.LanguageServer.Extensions;

namespace Microsoft.Python.LanguageServer.Implementation {
    partial class Server {
        private readonly ConcurrentDictionary<string,
            PythonTools.Analysis.LanguageServer.ILanguageServerExtension> _oldExtensions =
                new ConcurrentDictionary<string, PythonTools.Analysis.LanguageServer.ILanguageServerExtension>();
        private PythonTools.Analysis.LanguageServer.Server _oldServer;

        public async Task LoadExtensionAsync(PythonAnalysisExtensionParams extension, IServiceContainer services, CancellationToken cancellationToken) {
            if (!await LoadExtensionAsyncOld(extension, cancellationToken)) {
                await LoadExtensionAsyncNew(extension, services, cancellationToken);
            }
        }
        private async Task<bool> LoadExtensionAsyncOld(PythonAnalysisExtensionParams extension, CancellationToken cancellationToken) {
            try {
                var provider = ActivateObject<PythonTools.Analysis.LanguageServer.ILanguageServerExtensionProvider>(extension.assembly, extension.typeName, null);
                if (provider == null) {
                    return false;
                }

                _oldServer = new PythonTools.Analysis.LanguageServer.Server();
                var ext = await provider.CreateAsync(_oldServer, extension.properties ?? new Dictionary<string, object>(), cancellationToken);
                if (ext == null) {
                    LogMessage(MessageType.Error, $"Extension provider {extension.assembly} {extension.typeName} returned null");
                    return false;
                }

                string n = null;
                try {
                    n = ext.Name;
                } catch (NotImplementedException) {
                } catch (NotSupportedException) {
                }

                if (!string.IsNullOrEmpty(n)) {
                    _oldExtensions.AddOrUpdate(n, ext, (_, previous) => {
                        (previous as IDisposable)?.Dispose();
                        return ext;
                    });
                }
                return true;
            } catch (Exception) {
            }
            return false;
        }

        private async Task LoadExtensionAsyncNew(PythonAnalysisExtensionParams extension, IServiceContainer services, CancellationToken cancellationToken) {
            try {
                var provider = ActivateObject<ILanguageServerExtensionProvider>(extension.assembly, extension.typeName, null);
                if (provider == null) {
                    LogMessage(MessageType.Error, $"Extension provider {extension.assembly} {extension.typeName} failed to load");
                    return;
                }

                var ext = await provider.CreateAsync(this, extension.properties ?? new Dictionary<string, object>(), cancellationToken);
                if (ext == null) {
                    LogMessage(MessageType.Error, $"Extension provider {extension.assembly} {extension.typeName} returned null");
                    return;
                }

                string n = null;
                try {
                    n = ext.Name;
                    await ext.Initialize(services, cancellationToken);
                } catch (NotImplementedException) {
                } catch (NotSupportedException) {
                }

                if (!string.IsNullOrEmpty(n)) {
                    _extensions.AddOrUpdate(n, ext, (_, previous) => {
                        (previous as IDisposable)?.Dispose();
                        return ext;
                    });
                }
            } catch (Exception ex) {
                LogMessage(MessageType.Error, $"Error loading extension {extension.typeName} from'{extension.assembly}': {ex}");
            }
        }

        public async Task<ExtensionCommandResult> ExtensionCommand(ExtensionCommandParams @params, CancellationToken token) {
            if (string.IsNullOrEmpty(@params.extensionName)) {
                throw new ArgumentNullException(nameof(@params.extensionName));
            }

            if (!_extensions.TryGetValue(@params.extensionName, out var ext)) {
                throw new LanguageServerException(LanguageServerException.UnknownExtension, "No extension loaded with name: " + @params.extensionName);
            }

            return new ExtensionCommandResult {
                properties = await ext?.ExecuteCommand(@params.command, @params.properties, token)
            };
        }

        private async Task InvokeExtensionsAsync(Func<ILanguageServerExtension, CancellationToken, Task> action, CancellationToken cancellationToken) {
            foreach (var ext in _extensions) {
                try {
                    if (cancellationToken.IsCancellationRequested) {
                        break;
                    }
                    await action(ext.Value, cancellationToken);
                } catch (Exception ex) when (!ex.IsCriticalException() && !(ex is OperationCanceledException)) {
                    LogMessage(MessageType.Error, $"Error invoking extension '{ext.Key}': {ex}");
                }
            }
        }
    }
}
