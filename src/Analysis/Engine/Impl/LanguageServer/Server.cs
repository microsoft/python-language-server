// Python Tools for Visual Studio
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
using Microsoft.Python.Core;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    [Obsolete("Implement Microsoft.Python.LanguageServer.Extensions.ILanguageServerExtension model")]
    public class Server : IServer {
        public event EventHandler<Extensibility.CompletionEventArgs> PostProcessCompletion;

        public void LogMessage(MessageType type, string message) { }

        public void ProcessCompletionList(ModuleAnalysis analysis, PythonAst tree, SourceLocation location, CompletionList completions) {
            var evt = PostProcessCompletion;
            if (evt != null) {
                var e = new Extensibility.CompletionEventArgs(analysis, tree, location, completions);
                try {
                    evt(this, e);
                    completions = e.CompletionList;
                    completions.items = completions.items ?? Array.Empty<CompletionItem>();
                } catch (Exception ex) when (!ex.IsCriticalException()) {
                    // We do not replace res in this case.
                }
            }
        }
    }
}
