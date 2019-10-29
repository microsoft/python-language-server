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
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.Analysis.Diagnostics {
    public sealed class DiagnosticsEntry {
        public DiagnosticsEntry(string message,
                                SourceSpan span,
                                string errorCode,
                                Severity severity,
                                DiagnosticSource source,
                                DiagnosticTags[] tags = null) {
            Message = message;
            SourceSpan = span;
            ErrorCode = errorCode;
            Severity = severity;
            Source = source;
            Tags = tags ?? Array.Empty<DiagnosticTags>();
        }

        /// <summary>
        /// Human-readable, localizable message.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Location of the diagnostics.
        /// </summary>
        public SourceSpan SourceSpan { get; }

        /// <summary>
        /// Error code: non-localizable, unique identifier for the problem.
        /// </summary>
        public string ErrorCode { get; }

        /// <summary>
        /// Issue severity.
        /// </summary>
        public Severity Severity { get; }

        /// <summary>
        /// Subsystem that produced the diagnostics.
        /// </summary>
        public DiagnosticSource Source { get; }

        public DiagnosticTags[] Tags { get; }

        public DiagnosticsEntry WithSeverity(Severity severity) {
            return new DiagnosticsEntry(Message, SourceSpan, ErrorCode, severity, Source, Tags);
        }

        public bool ShouldReport(IPythonModule module) {
            // Only report for user written modules
            if (module.ModuleType != ModuleType.User) {
                return false;
            }

            // If user specifies #noqa, then do not report diagnostic
            if (module.GetComment(SourceSpan.Start.Line).EqualsIgnoreCase("noqa")) {
                return false;
            }

            return true;
        }

        public override bool Equals(object obj) {
            if (!(obj is DiagnosticsEntry e)) {
                return false;
            }

            // for now, we ignore tags equality since we don't want to show duplicated errors
            // just because tags are different
            return ErrorCode == e.ErrorCode && SourceSpan == e.SourceSpan;
        }
        public override int GetHashCode() => 0;

        /// <summary>
        /// Value of each enum member must match the one in <see cref="DiagnosticTags" />
        /// </summary>
        public enum DiagnosticTags {
            /// <summary>
            /// Unused or unnecessary code.
            /// 
            /// Clients are allowed to render diagnostics with this tag faded out instead of having
            /// an error squiggle.
            /// </summary>
            Unnecessary = 1,

            /// <summary>
            /// Deprecated or obsolete code.
            ///
            /// Clients are allowed to rendered diagnostics with this tag strike through.
            /// </summary>
            Deprecated = 2
        }
    }
}
