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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Core;
using Microsoft.Python.Core.OS;
using Microsoft.Python.LanguageServer.Protocol;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;

namespace Microsoft.Python.LanguageServer.Implementation {
    public sealed partial class LanguageServer {
        [JsonRpcMethod("workspace/didChangeConfiguration")]
        public async Task DidChangeConfiguration(JToken token, CancellationToken cancellationToken) {
            using (await _prioritizer.ConfigurationPriorityAsync(cancellationToken)) {
                var settings = new LanguageServerSettings();

                var rootSection = token["settings"];
                var pythonSection = rootSection?["python"];
                if (pythonSection == null) {
                    return;
                }

                var autoComplete = pythonSection["autoComplete"];
                settings.completion.showAdvancedMembers = GetSetting(autoComplete, "showAdvancedMembers", true);
                settings.completion.addBrackets = GetSetting(autoComplete, "addBrackets", false);

                var analysis = pythonSection["analysis"];
                settings.symbolsHierarchyDepthLimit = GetSetting(analysis, "symbolsHierarchyDepthLimit", 10);
                settings.symbolsHierarchyMaxSymbols = GetSetting(analysis, "symbolsHierarchyMaxSymbols", 1000);

                _logger.LogLevel = GetLogLevel(analysis).ToTraceEventType();

                var autoCompleteExtraPaths = GetSetting<IReadOnlyList<string>>(autoComplete, "extraPaths", Array.Empty<string>());
                var analysisSearchPaths = GetSetting<IReadOnlyList<string>>(analysis, "searchPaths", null);
                var analysisUsePYTHONPATH = GetSetting(analysis, "usePYTHONPATH", true);

                var userConfiguredPaths = analysisSearchPaths ?? autoCompleteExtraPaths;
                if (analysisUsePYTHONPATH) {
                    var pythonpath = Environment.GetEnvironmentVariable("PYTHONPATH");
                    if (pythonpath != null) {
                        var sep = _services.GetService<IOSPlatform>().IsWindows ? ';' : ':';
                        var paths = pythonpath.Split(sep, StringSplitOptions.RemoveEmptyEntries);
                        userConfiguredPaths = userConfiguredPaths.Concat(paths).ToList();
                    }
                }

                HandleUserConfiguredPathsChanges(userConfiguredPaths);
                HandlePathWatchChanges(GetSetting(analysis, "watchSearchPaths", true));
                HandleDiagnosticsChanges(pythonSection, settings);

                _server.DidChangeConfiguration(new DidChangeConfigurationParams { settings = settings }, cancellationToken);
            }
        }

        private void HandleDiagnosticsChanges(JToken pythonSection, LanguageServerSettings settings) {
            var analysis = pythonSection["analysis"];

            settings.diagnosticPublishDelay = GetSetting(analysis, "diagnosticPublishDelay", 1000);
            var ds = _services.GetService<IDiagnosticsService>();
            ds.PublishingDelay = settings.diagnosticPublishDelay;

            ds.DiagnosticsSeverityMap = new DiagnosticsSeverityMap(
                GetSetting(analysis, "errors", Array.Empty<string>()),
                GetSetting(analysis, "warnings", Array.Empty<string>()),
                GetSetting(analysis, "information", Array.Empty<string>()),
                GetSetting(analysis, "disabled", Array.Empty<string>()));

            var linting = pythonSection["linting"];
            HandleLintingOnOff(_services, GetSetting(linting, "enabled", true));

            var memory = analysis["memory"];
            var optionsProvider = _services.GetService<IAnalysisOptionsProvider>();
            optionsProvider.Options.KeepLibraryLocalVariables = GetSetting(memory, "keepLibraryLocalVariables", false);
            optionsProvider.Options.KeepLibraryAst = GetSetting(memory, "keepLibraryAst", false);
        }

        internal static void HandleLintingOnOff(IServiceContainer services, bool linterEnabled) {
            var optionsProvider = services.GetService<IAnalysisOptionsProvider>();
            var ds = services.GetService<IDiagnosticsService>();
            var rdt = services.GetService<IRunningDocumentTable>();

            var wasEnabled = optionsProvider.Options.LintingEnabled;
            optionsProvider.Options.LintingEnabled = linterEnabled;

            foreach (var m in rdt.GetDocuments().Where(m => m.ModuleType == ModuleType.User)) {
                IReadOnlyList<DiagnosticsEntry> entries = Array.Empty<DiagnosticsEntry>();
                if (!wasEnabled && linterEnabled) {
                    // Lint all user files in the RDT
                    var analyzer = services.GetService<IPythonAnalyzer>();
                    entries = analyzer.LintModule(m);
                }
                ds.Replace(m.Uri, entries, DiagnosticSource.Linter);
            }
        }

        private void HandlePathWatchChanges(bool watchSearchPaths) => _server.HandleWatchPathsChange(watchSearchPaths);

        private void HandleUserConfiguredPathsChanges(IReadOnlyList<string> paths) => _server.HandleUserConfiguredPathsChange(paths);
    }
}
