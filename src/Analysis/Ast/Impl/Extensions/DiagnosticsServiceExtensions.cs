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
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.Analysis {
    public static class DiagnosticsServiceExtensions {
        public static void Add(this IDiagnosticsService ds, Uri documentUri, string message, SourceSpan span, string errorCode, Severity severity)
            => ds.Add(documentUri, new DiagnosticsEntry(message, span, errorCode, severity));

        public static void Add(this IDiagnosticsService ds, Uri documentUri, IEnumerable<DiagnosticsEntry> entries) {
            foreach (var e in entries) {
                ds.Add(documentUri, e);
            }
        }
    }
}
