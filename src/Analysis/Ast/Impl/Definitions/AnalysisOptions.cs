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

namespace Microsoft.Python.Analysis {
    public enum AnalysisCachingLevel {
        /// <summary>
        /// No caching of analysis.
        /// </summary>
        None,

        /// <summary>
        /// Cache analysis results of system (language) modules.
        /// Do not cache user-installed modules or site-packages.
        /// </summary>
        System,

        /// <summary>
        /// Full caching, includes system and library modules.
        /// Does not enable caching of user code analysis.
        /// </summary>
        Library
    }

    public class AnalysisOptions {
        public bool LintingEnabled { get; set; }

        /// <summary>
        /// Keep in memory information on local variables declared in
        /// functions in libraries. Provides ability to navigate to
        /// symbols used in function bodies in packages and libraries.
        /// </summary>
        public bool KeepLibraryLocalVariables { get; set; }

        /// <summary>
        /// Keep in memory AST of library source code. May somewhat
        /// improve performance when library code has to be re-analyzed.
        /// </summary>
        public bool KeepLibraryAst { get; set; }

        /// <summary>
        /// Defines level of caching analysis engine will maintain.
        /// </summary>
        public AnalysisCachingLevel AnalysisCachingLevel { get; set; }

        /// <summary>
        /// Tells if source module should be analyzed or, if stub is present,
        /// the stub becomes primary source of information on types and source
        /// modules would be used only as documentation provider.
        /// </summary>
        public bool StubOnlyAnalysis { get; set; }
    }
}
