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
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.OS;
using Microsoft.Python.LanguageServer.CodeActions;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.LanguageServer.SearchPaths;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;
using DiagnosticSource = Microsoft.Python.Analysis.Diagnostics.DiagnosticSource;

namespace Microsoft.Python.LanguageServer.Implementation {
    public sealed partial class LanguageServer {
        [JsonRpcMethod("workspace/didChangeConfiguration")]
        public async Task DidChangeConfiguration(JToken token, CancellationToken cancellationToken) {
            using (await _prioritizer.ConfigurationPriorityAsync(cancellationToken)) {
                Debug.Assert(_initialized);

                var settings = new LanguageServerSettings();

                // https://github.com/microsoft/python-language-server/issues/915
                // If token or settings are missing, assume defaults.
                var rootSection = token?["settings"];
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

                var userConfiguredPaths = GetUserConfiguredPaths(pythonSection);

                HandleUserConfiguredPathsChanges(userConfiguredPaths);
                HandlePathWatchChanges(GetSetting(analysis, "watchSearchPaths", true));
                HandleDiagnosticsChanges(pythonSection, settings);
                HandleCodeActionsChanges(pythonSection);

                _server.DidChangeConfiguration(new DidChangeConfigurationParams { settings = settings }, cancellationToken);
            }
        }

        private void HandleCodeActionsChanges(JToken pythonSection) {
            var refactoring = new Dictionary<string, object>();
            var quickFix = new Dictionary<string, object>();

            var refactoringToken = pythonSection["refactoring"];
            var quickFixToken = pythonSection["quickfix"];

            // +1 is for last "." after prefix
            AppendToMap(refactoringToken, refactoringToken?.Path.Length + 1 ?? 0, refactoring);
            AppendToMap(quickFixToken, quickFixToken?.Path.Length + 1 ?? 0, quickFix);

            var codeActionSettings = new CodeActionSettings(refactoring, quickFix);
            _server.HandleCodeActionsChange(codeActionSettings);

            void AppendToMap(JToken setting, int prefixLength, Dictionary<string, object> map) {
                if (setting == null || !setting.HasValues) {
                    return;
                }

                foreach (var child in setting) {
                    if (child is JValue value) {
                        // there shouldn't be duplicates and prefix must exist.
                        var path = child.Path;
                        if (path.Length <= prefixLength) {
                            // nothing to add
                            continue;
                        }

                        // get rid of common "settings.python..." prefix
                        map[path.Substring(prefixLength)] = value.Value;
                        continue;
                    }

                    AppendToMap(child, prefixLength, map);
                }
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
            optionsProvider.Options.AnalysisCachingLevel = GetAnalysisCachingLevel(analysis);

            _logger?.Log(TraceEventType.Information, Resources.AnalysisCacheLevel.FormatInvariant(optionsProvider.Options.AnalysisCachingLevel));
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

        private void HandleUserConfiguredPathsChanges(ImmutableArray<string> paths) => _server.HandleUserConfiguredPathsChange(paths);

        /// <summary>
        /// Gets the user's configured search paths, by python.analysis.searchPaths,
        /// python.autoComplete.extraPaths, PYTHONPATH, or _initParam's searchPaths.
        /// </summary>
        /// <param name="pythonSection">The python section of the user config.</param>
        /// <returns>An array of search paths.</returns>
        private ImmutableArray<string> GetUserConfiguredPaths(JToken pythonSection) {
            var paths = ImmutableArray<string>.Empty;
            var set = false;

            if (pythonSection != null) {
                var autoComplete = pythonSection["autoComplete"];
                var analysis = pythonSection["analysis"];

                // The values of these may not be null even if the value is "unset", depending on
                // what the client uses as a default. Use null as a default anyway until the
                // extension uses a null default (and/or extraPaths is dropped entirely).
                var autoCompleteExtraPaths = GetSetting<IReadOnlyList<string>>(autoComplete, "extraPaths", null);
                var analysisSearchPaths = GetSetting<IReadOnlyList<string>>(analysis, "searchPaths", null);
                var analysisUsePYTHONPATH = GetSetting(analysis, "usePYTHONPATH", true);
                var analayisAutoSearchPaths = GetSetting(analysis, "autoSearchPaths", true);

                if (analysisSearchPaths != null) {
                    set = true;
                    paths = analysisSearchPaths.ToImmutableArray();
                } else if (autoCompleteExtraPaths != null) {
                    set = true;
                    paths = autoCompleteExtraPaths.ToImmutableArray();
                }

                if (analysisUsePYTHONPATH) {
                    var pythonpath = Environment.GetEnvironmentVariable("PYTHONPATH");
                    if (pythonpath != null) {
                        var sep = _services.GetService<IOSPlatform>().IsWindows ? ';' : ':';
                        var pythonpathPaths = pythonpath.Split(sep, StringSplitOptions.RemoveEmptyEntries);
                        if (pythonpathPaths.Length > 0) {
                            paths = paths.AddRange(pythonpathPaths);
                            set = true;
                        }
                    }
                }

                if (analayisAutoSearchPaths) {
                    var fs = _services.GetService<IFileSystem>();
                    var auto = AutoSearchPathFinder.Find(fs, _server.Root);
                    paths = paths.AddRange(auto);
                    set = true;
                }
            }

            if (set) {
                return paths;
            }

            var initPaths = _initParams?.initializationOptions?.searchPaths;
            if (initPaths != null) {
                return initPaths.ToImmutableArray();
            }

            return ImmutableArray<string>.Empty;
        }

        private const string DefaultCachingLevel = "None";

        private AnalysisCachingLevel GetAnalysisCachingLevel(JToken analysisKey) {
            // TODO: Remove this one caching is working at any level again.
            // https://github.com/microsoft/python-language-server/issues/1758
            return AnalysisCachingLevel.None;

            // var s = GetSetting(analysisKey, "cachingLevel", DefaultCachingLevel);
            // 
            // if (string.IsNullOrWhiteSpace(s) || s.EqualsIgnoreCase("Default")) {
            //     s = DefaultCachingLevel;
            // }
            // 
            // if (s.EqualsIgnoreCase("System")) {
            //     return AnalysisCachingLevel.System;
            // }
            // 
            // return s.EqualsIgnoreCase("Library") ? AnalysisCachingLevel.Library : AnalysisCachingLevel.None;
        }
    }
}
