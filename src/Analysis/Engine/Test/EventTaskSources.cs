﻿// Python Tools for Visual Studio
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

using TestUtilities;

namespace AnalysisTests {
    internal static class EventTaskSources {
        public static class AstPythonInterpreter {
            public static readonly EventTaskSource<Microsoft.PythonTools.Interpreter.Ast.AstPythonInterpreter> ModuleNamesChanged =
                new EventTaskSource<Microsoft.PythonTools.Interpreter.Ast.AstPythonInterpreter>(
                    (o, e) => o.ModuleNamesChanged += e,
                    (o, e) => o.ModuleNamesChanged -= e);
        }

        public static class AnalysisQueue {
            public static readonly EventTaskSource<Microsoft.PythonTools.Intellisense.AnalysisQueue> AnalysisComplete =
                new EventTaskSource<Microsoft.PythonTools.Intellisense.AnalysisQueue>(
                    (o, e) => o.AnalysisComplete += e,
                    (o, e) => o.AnalysisComplete -= e);
        }

        public static class Server {
            public static readonly EventTaskSource<Microsoft.Python.LanguageServer.Implementation.Server, Microsoft.Python.LanguageServer.ParseCompleteEventArgs> OnParseComplete =
                new EventTaskSource<Microsoft.Python.LanguageServer.Implementation.Server, Microsoft.Python.LanguageServer.ParseCompleteEventArgs>(
                    (o, e) => o.OnParseComplete += e,
                    (o, e) => o.OnParseComplete -= e);
        }
    }
}
