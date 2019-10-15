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

using Microsoft.Python.Core.Text;

namespace Microsoft.Python.LanguageServer.Protocol {
    public class Diagnostic {
        /// <summary>
        /// The range at which the message applies.
        /// </summary>
        public Range range;

        /// <summary>
        /// The diagnostics severity.
        /// </summary>
        public DiagnosticSeverity severity;

        /// <summary>
        /// The diagnostics code (string, such as 'unresolved-import').
        /// </summary>
        public string code;

        /// <summary>
        /// A human-readable string describing the source of this
        /// diagnostic, e.g. 'typescript' or 'super lint'.
        /// </summary>
        public string source;

        /// <summary>
        /// The diagnostics message.
        /// </summary>
        public string message;

        /// <summary>
        /// Additional metadata about the diagnostic.
        /// </summary>
        public DiagnosticTag[] tags;
    }

    public enum DiagnosticSeverity : int {
        Unspecified = 0,
        Error = 1,
        Warning = 2,
        Information = 3,
        Hint = 4
    }

    public enum DiagnosticTag : int {
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
