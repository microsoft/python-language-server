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
    internal sealed class PlainTextDocumentationSource : DocumentationSource, IDocumentationSource {
        public InsertTextFormat DocumentationFormat => InsertTextFormat.PlainText;

        public MarkupContent GetHover(string name, IMember member, IPythonType self) {
            // We need to tell between instance and type.
            var type = member.GetPythonType();
            if (type.IsUnknown()) {
                return new MarkupContent { kind = MarkupKind.PlainText, value = name };
            }

            string text;
            if (member is IPythonInstance) {
                if (!(type is IPythonFunctionType)) {
                    text = !string.IsNullOrEmpty(name) ? $"{name}: {type.Name}" : $"{type.Name}";
                    return new MarkupContent { kind = MarkupKind.PlainText, value = text };
                }
            }

            var typeDoc = !string.IsNullOrEmpty(type.Documentation) ? $"\n\n{type.PlaintextDoc()}" : string.Empty;
            switch (type) {
                case IPythonPropertyType prop:
                    text = GetPropertyHoverString(prop);
                    break;

                case IPythonFunctionType ft:
                    text = GetFunctionHoverString(ft, self);
                    break;

                case IPythonClassType cls:
                    var clsDoc = !string.IsNullOrEmpty(cls.Documentation) ? $"\n\n{cls.PlaintextDoc()}" : string.Empty;
                    text = $"class {cls.Name}{clsDoc}";
                    break;

                case IPythonModule mod:
                    text = !string.IsNullOrEmpty(mod.Name) ? $"module {mod.Name}{typeDoc}" : $"module{typeDoc}";
                    break;

                default:
                    text = !string.IsNullOrEmpty(name) ? $"type {name}: {type.Name}{typeDoc}" : $"{type.Name}{typeDoc}";
                    break;
            }

            return new MarkupContent {
                kind = MarkupKind.PlainText, value = text
            };
        }

        public MarkupContent FormatDocumentation(string documentation) 
            => new MarkupContent { kind = MarkupKind.PlainText, value = documentation };

        public MarkupContent FormatParameterDocumentation(IParameterInfo parameter) {
            if (!string.IsNullOrEmpty(parameter.Documentation)) {
                return FormatDocumentation(parameter.Documentation);
            }
            // TODO: show fully qualified type?
            var text = parameter.Type.IsUnknown() ? parameter.Name : $"{parameter.Name}: {parameter.Type.Name}";
            return new MarkupContent { kind = MarkupKind.PlainText, value = text };
        }

        private string GetPropertyHoverString(IPythonPropertyType prop, int overloadIndex = 0) {
            var decTypeString = prop.DeclaringType != null ? $"{prop.DeclaringType.Name}." : string.Empty;
            var propDoc = !string.IsNullOrEmpty(prop.Documentation) ? $"\n\n{prop.PlaintextDoc()}" : string.Empty;
            return $"{decTypeString}{propDoc}";
        }

        private string GetFunctionHoverString(IPythonFunctionType ft, IPythonType self, int overloadIndex = 0) {
            var sigString = GetSignatureString(ft, self, out _, overloadIndex);
            var decTypeString = ft.DeclaringType != null ? $"{ft.DeclaringType.Name}." : string.Empty;
            var funcDoc = !string.IsNullOrEmpty(ft.Documentation) ? $"\n\n{ft.PlaintextDoc()}" : string.Empty;
            return $"{decTypeString}{sigString}{funcDoc}";
        }
    }
}
