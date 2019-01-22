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

using System.Linq;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Tests;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Completion;
using Microsoft.Python.LanguageServer.Documentation;
using Microsoft.Python.LanguageServer.Protocol;

namespace Microsoft.Python.LanguageServer.Tests {
    public abstract class LanguageServerTestBase : AnalysisTestBase {
        protected static readonly IDocumentationSource TestDocumentationSource = new TestDocSource();
        protected static readonly ServerSettings ServerSettings = new ServerSettings();

        internal async Task<CompletionItem[]> GetCompletionsAsync(string code, int line, int column) {
            var analysis = await GetAnalysisAsync(code);
            var cs = new CompletionSource(TestDocumentationSource, ServerSettings.completion);
            return (await cs.GetCompletionsAsync(analysis, new SourceLocation(line, column))).Completions.ToArray();
        }

        protected sealed class TestDocSource : IDocumentationSource {
            public InsertTextFormat DocumentationFormat => InsertTextFormat.PlainText;
            public MarkupContent GetDocumentation(IPythonType type)
                => new MarkupContent { kind = MarkupKind.PlainText, value = type.Documentation ?? type.Name };
        }
    }
}
