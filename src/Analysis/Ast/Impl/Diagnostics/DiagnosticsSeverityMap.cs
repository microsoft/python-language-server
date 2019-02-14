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

using System.Collections.Generic;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.Analysis.Diagnostics {
    public sealed class DiagnosticsSeverityMap {
        private readonly Dictionary<string, Severity> _map = new Dictionary<string, Severity>();

        public DiagnosticsSeverityMap() { }

        public DiagnosticsSeverityMap(string[] errors, string[] warnings, string[] information, string[] disabled) {
            _map.Clear();
            // disabled > error > warning > information
            foreach (var x in information.MaybeEnumerate()) {
                _map[x] = Severity.Information;
            }
            foreach (var x in warnings.MaybeEnumerate()) {
                _map[x] = Severity.Warning;
            }
            foreach (var x in errors.MaybeEnumerate()) {
                _map[x] = Severity.Error;
            }
            foreach (var x in disabled.MaybeEnumerate()) {
                _map[x] = Severity.Suppressed;
            }
        }
        public Severity GetEffectiveSeverity(string code, Severity defaultSeverity)
            => _map.TryGetValue(code, out var severity) ? severity : defaultSeverity;
    }
}
