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

using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.LanguageServer.Documentation;
using Microsoft.Python.LanguageServer.Protocol;

namespace Microsoft.Python.LanguageServer.Completion {
    internal class CompletionItemSource {
        public static readonly CompletionItem FromKeyword = CreateCompletionItem("from", CompletionItemKind.Keyword);
        public static readonly CompletionItem ImportKeyword = CreateCompletionItem("import", CompletionItemKind.Keyword);
        public static readonly CompletionItem MetadataArg = CreateCompletionItem(@"metaclass=", CompletionItemKind.TypeParameter);
        public static readonly CompletionItem AsKeyword = CreateCompletionItem("as", CompletionItemKind.Keyword);
        public static readonly CompletionItem InKeyword = CreateCompletionItem("in", CompletionItemKind.Keyword);
        public static readonly CompletionItem WithKeyword = CreateCompletionItem("with", CompletionItemKind.Keyword);
        public static readonly CompletionItem Star = CreateCompletionItem("*", CompletionItemKind.Keyword);

        private readonly IDocumentationSource _docSource;
        private readonly ServerSettings.PythonCompletionOptions _options;

        public CompletionItemSource(IDocumentationSource docSource, ServerSettings.PythonCompletionOptions options) {
            _docSource = docSource;
            _options = options;
        }

        public CompletionItem CreateCompletionItem(string text, IMember member, string label = null)
            => CreateCompletionItem(text, ToCompletionItemKind(member.MemberType), member, label);

        public CompletionItem CreateCompletionItem(string text, CompletionItemKind kind, IMember member, string label = null) {
            var t = member.GetPythonType();
            var docFormat = _docSource.DocumentationFormat;

            if (_options.addBrackets && (kind == CompletionItemKind.Constructor || kind == CompletionItemKind.Function || kind == CompletionItemKind.Method)) {
                text += "($0)";
                docFormat = InsertTextFormat.Snippet;
            }

            return new CompletionItem {
                label = label ?? text,
                insertText = text,
                insertTextFormat = docFormat,
                // Place regular items first, advanced entries last
                sortText = char.IsLetter(text, 0) ? "1" : "2",
                kind = kind,
                documentation = t != null ? _docSource.GetTypeDocumentation(label ?? text, t) : null
            };
        }

        public static CompletionItem CreateCompletionItem(string text, CompletionItemKind kind)
            => new CompletionItem {
                label = text, insertText = text, insertTextFormat = InsertTextFormat.PlainText,
                sortText = char.IsLetter(text, 0) ? "1" : "2", kind = kind
            };

        private static CompletionItemKind ToCompletionItemKind(PythonMemberType memberType) {
            switch (memberType) {
                case PythonMemberType.Unknown: return CompletionItemKind.None;
                case PythonMemberType.Class: return CompletionItemKind.Class;
                case PythonMemberType.Instance: return CompletionItemKind.Value;
                case PythonMemberType.Function: return CompletionItemKind.Function;
                case PythonMemberType.Method: return CompletionItemKind.Method;
                case PythonMemberType.Module: return CompletionItemKind.Module;
                case PythonMemberType.Property: return CompletionItemKind.Property;
                case PythonMemberType.Union: return CompletionItemKind.Struct;
                case PythonMemberType.Variable: return CompletionItemKind.Variable;
                case PythonMemberType.Generic: return CompletionItemKind.TypeParameter;
            }
            return CompletionItemKind.None;
        }
    }
}
