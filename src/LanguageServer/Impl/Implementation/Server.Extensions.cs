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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Core;
using Microsoft.Python.LanguageServer.Extensibility;
using Microsoft.Python.LanguageServer.Extensions;
using Microsoft.Python.LanguageServer.Protocol;

namespace Microsoft.Python.LanguageServer.Implementation {
    partial class Server {
        private readonly ConcurrentDictionary<string, ILanguageServerExtension> _extensions = new ConcurrentDictionary<string, ILanguageServerExtension>();
        public async Task LoadExtensionAsync(PythonAnalysisExtensionParams extension, IServiceContainer services, CancellationToken cancellationToken) {
            try {
                var provider = ActivateObject<ILanguageServerExtensionProvider>(extension.assembly, extension.typeName, null);
                if (provider == null) {
                    _log?.Log(TraceEventType.Error, $"Extension provider {extension.assembly} {extension.typeName} failed to load");
                    return;
                }

                var ext = await provider.CreateAsync(_services, extension.properties ?? new Dictionary<string, object>(), cancellationToken);
                if (ext == null) {
                    _log?.Log(TraceEventType.Error, $"Extension provider {extension.assembly} {extension.typeName} returned null");
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
                        previous?.Dispose();
                        return ext;
                    });
                }
            } catch (Exception ex) {
                _log?.Log(TraceEventType.Error, $"Error loading extension {extension.typeName} from'{extension.assembly}': {ex}");
            }
        }

        public async Task<ExtensionCommandResult> ExtensionCommandAsync(ExtensionCommandParams @params, CancellationToken token) {
            if (string.IsNullOrEmpty(@params.extensionName)) {
                throw new ArgumentNullException(nameof(@params.extensionName));
            }

            if (!_extensions.TryGetValue(@params.extensionName, out var ext)) {
                throw new LanguageServerException(LanguageServerException.UnknownExtension, "No extension loaded with name: " + @params.extensionName);
            }

            if (ext != null) {
                return new ExtensionCommandResult {
                    properties = await ext.ExecuteCommand(@params.command, @params.properties, token)
                }; 
            }
            return new ExtensionCommandResult();
        }

        private async Task InvokeExtensionsAsync(Func<ILanguageServerExtension, CancellationToken, Task> action, CancellationToken cancellationToken) {
            foreach (var ext in _extensions) {
                try {
                    if (cancellationToken.IsCancellationRequested) {
                        break;
                    }
                    await action(ext.Value, cancellationToken);
                } catch (Exception ex) when (!ex.IsCriticalException() && !(ex is OperationCanceledException)) {
                    _log?.Log(TraceEventType.Error, $"Error invoking extension '{ext.Key}': {ex}");
                }
            }
        }

        private T ActivateObject<T>(string assemblyName, string typeName, Dictionary<string, object> properties) {
            if (string.IsNullOrEmpty(assemblyName) || string.IsNullOrEmpty(typeName)) {
                return default(T);
            }
            try {
                var assembly = File.Exists(assemblyName)
                    ? Assembly.LoadFrom(assemblyName)
                    : Assembly.Load(new AssemblyName(assemblyName));

                var type = assembly.GetType(typeName, true);

                return (T)Activator.CreateInstance(
                    type,
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance,
                    null,
                    properties == null ? Array.Empty<object>() : new object[] { properties },
                    CultureInfo.CurrentCulture
                );
            } catch (Exception ex) {
                _log?.Log(TraceEventType.Warning, ex.ToString());
            }

            return default;
        }
    }
}
