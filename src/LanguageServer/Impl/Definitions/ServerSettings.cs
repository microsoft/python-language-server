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
using Microsoft.Python.LanguageServer.Protocol;

namespace Microsoft.Python.LanguageServer {
    public class ServerSettings {
        public class PythonAnalysisOptions {
            private readonly Dictionary<string, DiagnosticSeverity> _map = new Dictionary<string, DiagnosticSeverity>();

            public int symbolsHierarchyDepthLimit = 10;
            public int symbolsHierarchyMaxSymbols = 1000;

            public string[] errors { get; private set; } = Array.Empty<string>();
            public string[] warnings { get; private set; } = Array.Empty<string>();
            public string[] information { get; private set; } = Array.Empty<string>();
            public string[] disabled { get; private set; } = Array.Empty<string>();

            public DiagnosticSeverity GetEffectiveSeverity(string code, DiagnosticSeverity defaultSeverity)
                => _map.TryGetValue(code, out var severity) ? severity : defaultSeverity;

            public void SetErrorSeverityOptions(string[] errors, string[] warnings, string[] information, string[] disabled) {
                _map.Clear();
                // disabled > error > warning > information
                foreach (var x in information) {
                    _map[x] = DiagnosticSeverity.Information;
                }
                foreach (var x in warnings) {
                    _map[x] = DiagnosticSeverity.Warning;
                }
                foreach (var x in errors) {
                    _map[x] = DiagnosticSeverity.Error;
                }
                foreach (var x in disabled) {
                    _map[x] = DiagnosticSeverity.Unspecified;
                }

                this.errors = errors;
                this.warnings = warnings;
                this.information = information;
                this.disabled = disabled;
            }
        }
        public readonly PythonAnalysisOptions analysis = new PythonAnalysisOptions();

        public class PythonCompletionOptions {
            public bool showAdvancedMembers = true;
            public bool addBrackets = false;
        }

        public readonly PythonCompletionOptions completion = new PythonCompletionOptions();
    }
}
