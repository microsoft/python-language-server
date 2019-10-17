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
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.LanguageServer.Documentation;
using Microsoft.Python.LanguageServer.Protocol;

namespace Microsoft.Python.LanguageServer.Sources {
    internal sealed class MarkdownDocumentationSource : DocumentationSource, IDocumentationSource {
        public InsertTextFormat DocumentationFormat => InsertTextFormat.PlainText;

        public MarkupContent GetHover(string name, IMember member, IPythonType self, bool includeClassInit = false) {
            // We need to tell between instance and type.
            var type = member.GetPythonType();
            if (type.IsUnknown()) {
                return new MarkupContent { kind = MarkupKind.Markdown, value = $"```\n{name}\n```" };
            }

            string text;
            if (member is IPythonInstance) {
                if (!(type is IPythonFunctionType)) {
                    text = !string.IsNullOrEmpty(name) ? $"{name}: {type.Name}" : $"{type.Name}";
                    return new MarkupContent { kind = MarkupKind.Markdown, value = $"```\n{text}\n```" };
                }
            }

            var typeDoc = !string.IsNullOrEmpty(type.Documentation) ? $"\n---\n{type.MarkdownDoc()}" : string.Empty;
            switch (type) {
                case IPythonPropertyType prop:
                    text = GetPropertyString(prop);
                    break;

                case IPythonFunctionType ft:
                    text = GetFunctionString(ft, self);
                    break;

                case IPythonClassType cls:
                    var clsDoc = !string.IsNullOrEmpty(cls.Documentation) ? $"\n---\n{cls.MarkdownDoc()}" : string.Empty;

                    var sig = string.Empty;

                    if (includeClassInit) {
                        var init = cls.GetMember<IPythonFunctionType>("__init__");
                        if (init != null) {
                            sig = GetSignatureString(init, null, out var _, 0, "", true);
                        }
                    }

                    text = $"```\nclass {cls.Name}{sig}\n```{clsDoc}";
                    break;

                case IPythonModule mod:
                    text = !string.IsNullOrEmpty(mod.Name) ? $"```\nmodule {mod.Name}\n```{typeDoc}" : $"`module`{typeDoc}";
                    break;

                default:
                    text = !string.IsNullOrEmpty(name) ? $"```\ntype {name}: {type.Name}\n```{typeDoc}" : $"{type.Name}{typeDoc}";
                    break;
            }

            return new MarkupContent {
                kind = MarkupKind.Markdown, value = text
            };
        }

        public MarkupContent FormatDocumentation(string documentation)
            => new MarkupContent { kind = MarkupKind.Markdown, value = DocstringConverter.ToMarkdown(documentation) };

        public MarkupContent FormatParameterDocumentation(IParameterInfo parameter) {
            if (!string.IsNullOrEmpty(parameter.Documentation)) {
                return FormatDocumentation(parameter.Documentation);
            }
            // TODO: show fully qualified type?
            var text = parameter.Type.IsUnknown() ? $"```\n{parameter.Name}\n```" : $"`{parameter.Name}: {parameter.Type.Name}`";
            return new MarkupContent { kind = MarkupKind.Markdown, value = text };
        }

        private string GetPropertyString(IPythonPropertyType prop) {
            var decTypeString = prop.DeclaringType != null ? $"{prop.DeclaringType.Name}." : string.Empty;
            var propDoc = !string.IsNullOrEmpty(prop.Documentation) ? $"\n---\n{prop.MarkdownDoc()}" : string.Empty;
            return $"```\n{decTypeString}\n```{propDoc}";
        }

        private string GetFunctionString(IPythonFunctionType ft, IPythonType self, int overloadIndex = 0) {
            var sigString = GetSignatureString(ft, self, out _, overloadIndex);
            var decTypeString = ft.DeclaringType != null ? $"{ft.DeclaringType.Name}." : string.Empty;
            var funcDoc = !string.IsNullOrEmpty(ft.Documentation) ? $"\n---\n{ft.MarkdownDoc()}" : string.Empty;
            return $"```\n{decTypeString}{sigString}\n```{funcDoc}";
        }
    }
}
