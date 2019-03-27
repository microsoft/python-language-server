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

namespace Microsoft.Python.Analysis.Diagnostics {
    public interface IDiagnosticsService {
        /// <summary>
        /// Current complete diagnostics.
        /// </summary>
        IReadOnlyDictionary<Uri, IReadOnlyList<DiagnosticsEntry>> Diagnostics { get; }

        /// <summary>
        /// Replaces diagnostics for the document by the new set.
        /// </summary>
        void Replace(Uri documentUri, IEnumerable<DiagnosticsEntry> entries, DiagnosticSource source);

        /// <summary>
        /// Removes document from the diagnostics report. Typically when document is disposed.
        /// </summary>
        void Remove(Uri documentUri);

        /// <summary>
        /// Defines delay in milliseconds from the idle time start and
        /// the diagnostic publishing to the client.
        /// </summary>
        int PublishingDelay { get; set; }

        /// <summary>
        /// Provides map of error codes to severity when user wants
        /// to override default severity settings or suppress particular
        /// diagnostics completely.
        /// </summary>
        DiagnosticsSeverityMap DiagnosticsSeverityMap { get; set; }
    }
}
