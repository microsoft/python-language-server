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

    //
    // The kind of a code action.
    //
    // Kinds are a hierarchical list of identifiers separated by `.`, e.g. `"refactor.extract.function"`.
    //
    // The set of kinds is open and client needs to announce the kinds it supports to the server during
    // initialization.
    //
    // 
    // A set of predefined code action kinds
    // 
    public static class CodeActionKind {
        // 
        // Empty kind.
        // 
        public const string Empty = "";
        // 
        // Base kind for quickfix actions: 'quickfix'
        // 
        public const string QuickFix = "quickfix";
        // 
        // Base kind for refactoring actions: 'refactor'
        // 
        public const string Refactor = "refactor";
        // 
        // Base kind for refactoring extraction actions: 'refactor.extract'
        // 
        // Example extract actions:
        // 
        // - Extract method
        // - Extract function
        // - Extract variable
        // - Extract interface from class
        // - ...
        // 
        public const string RefactorExtract = "refactor.extract";
        // 
        // Base kind for refactoring inline actions: 'refactor.inline'
        // 
        // Example inline actions:
        // 
        // - Inline function
        // - Inline variable
        // - Inline constant
        // - ...
        // 
        public const string RefactorInline = "refactor.inline";
        // 
        // Base kind for refactoring rewrite actions: 'refactor.rewrite'
        // 
        // Example rewrite actions:
        // 
        // - Convert JavaScript function to class
        // - Add or remove parameter
        // - Encapsulate field
        // - Make method static
        // - Move method to base class
        // - ...
        // 
        public const string RefactorRewrite = "refactor.rewrite";
        // 
        // Base kind for source actions: `source`
        // 
        // Source code actions apply to the entire file.
        // 
        public const string Source = "source";
        // 
        // Base kind for an organize imports source action: `source.organizeImports`
        // 
        public const string SourceOrganizeImports = "source.organizeImports";
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
