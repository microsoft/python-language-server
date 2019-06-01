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

            public class AnalysisMemoryOptions {
                /// <summary>
                /// Keep in memory information on local variables declared in
                /// functions in libraries. Provides ability to navigate to
                /// symbols used in function bodies in packages and libraries.
                /// </summary>
                public bool keepLibraryLocalVariables;

                /// <summary>
                /// Keep in memory AST of library source code. May somewhat
                /// improve performance when library code has to be re-analyzed.
                /// </summary>
                public bool keepLibraryAst;
            }
            public AnalysisMemoryOptions memory;
        }
        public readonly PythonAnalysisOptions analysis = new PythonAnalysisOptions();

        public class PythonCompletionOptions {
            public bool showAdvancedMembers = true;
            public bool addBrackets = false;
        }
        public readonly PythonCompletionOptions completion = new PythonCompletionOptions();
    }
}
