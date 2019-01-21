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

using Microsoft.Python.Analysis.Types;
using Microsoft.Python.LanguageServer.Protocol;

namespace Microsoft.Python.LanguageServer.Completion {
    internal static class CompletionItemSource {
        public static readonly CompletionItem FromKeyword = CreateCompletionItem("from", CompletionItemKind.Keyword);
        public static readonly CompletionItem ImportKeyword = CreateCompletionItem("import", CompletionItemKind.Keyword);
        public static readonly CompletionItem MetadataArg = CreateCompletionItem(@"metaclass=", CompletionItemKind.TypeParameter);
        public static readonly CompletionItem AsKeyword = CreateCompletionItem("as", CompletionItemKind.Keyword);
        public static readonly CompletionItem InKeyword = CreateCompletionItem("in", CompletionItemKind.Keyword);
        public static readonly CompletionItem WithKeyword = CreateCompletionItem("with", CompletionItemKind.Keyword);
        public static readonly CompletionItem Star = CreateCompletionItem("*", CompletionItemKind.Keyword);

        public static CompletionItem CreateCompletionItem(string text, PythonMemberType memberType, string label = null)
            => CreateCompletionItem(text, ToCompletionItemKind(memberType), label);

        public static CompletionItem CreateCompletionItem(string text, CompletionItemKind kind, string label = null) {
            return new CompletionItem {
                label = label ?? text,
                insertText = text,
                insertTextFormat = InsertTextFormat.PlainText,
                // Place regular items first, advanced entries last
                sortText = char.IsLetter(text, 0) ? "1" : "2",
                kind = kind,
            };
        }

        private CompletionItem CreateCompletionItem(IMemberResult m) {
            var completion = m.Completion;
            if (string.IsNullOrEmpty(completion)) {
                completion = m.Name;
            }

            if (string.IsNullOrEmpty(completion)) {
                return default;
            }

            var doc = _textBuilder.GetDocumentation(m.Values, string.Empty);
            var kind = ToCompletionItemKind(m.MemberType);

            var res = new CompletionItem {
                label = m.Name,
                insertText = completion,
                insertTextFormat = InsertTextFormat.PlainText,
                documentation = string.IsNullOrWhiteSpace(doc) ? null : new MarkupContent {
                    kind = _textBuilder.DisplayOptions.preferredFormat,
                    value = doc
                },
                // Place regular items first, advanced entries last
                sortText = char.IsLetter(completion, 0) ? "1" : "2",
                kind = ToCompletionItemKind(m.MemberType),
            };

            if (_addBrackets && (kind == CompletionItemKind.Constructor || kind == CompletionItemKind.Function || kind == CompletionItemKind.Method)) {
                res.insertText += "($0)";
                res.insertTextFormat = InsertTextFormat.Snippet;
            }

            return res;
        }

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
