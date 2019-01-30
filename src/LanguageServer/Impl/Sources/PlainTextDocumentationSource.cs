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
using System.Linq;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.LanguageServer.Protocol;

namespace Microsoft.Python.LanguageServer.Sources {
    internal sealed class PlainTextDocumentationSource : IDocumentationSource {
        public InsertTextFormat DocumentationFormat => InsertTextFormat.PlainText;

        public MarkupContent GetHover(string name, IMember member) {
            var text = name;
            // We need to tell between instance and type.
            var type = member.GetPythonType();
            if (!type.IsUnknown()) {
                if (member is IPythonInstance && !(type is IPythonFunctionType)) {
                    text = !string.IsNullOrEmpty(name) ? $"{name}: {type.Name}" : $"{type.Name}";
                } else {
                    var typeDoc = !string.IsNullOrEmpty(type.Documentation) ? $"\n\n{type.Documentation}" : string.Empty;
                    switch (type) {
                        case IPythonPropertyType prop:
                            text = GetPropertyHoverString(prop);
                            break;

                        case IPythonFunctionType ft:
                            text = GetFunctionHoverString(ft);
                            break;

                        case IPythonClassType cls: {
                                var clsDoc = !string.IsNullOrEmpty(cls.Documentation) ? $"\n\n{cls.Documentation}" : string.Empty;
                                text = $"class {cls.Name}{clsDoc}";
                                break;
                            }

                        case IPythonModule _:
                            text = !string.IsNullOrEmpty(name) ? $"module {name}{typeDoc}" : $"{type.Name}{typeDoc}";
                            break;

                        default:
                            text = !string.IsNullOrEmpty(name) ? $"type {name}: {type.Name}{typeDoc}" : $"{type.Name}{typeDoc}";
                            break;
                    }
                }
            }

            return new MarkupContent { kind = MarkupKind.PlainText, value = text };
        }

        public MarkupContent FormatDocumentation(string documentation) {
            return new MarkupContent { kind = MarkupKind.PlainText, value = documentation };
        }

        public string GetSignatureString(IPythonFunctionType ft, int overloadIndex = 0) {
            var o = ft.Overloads[overloadIndex];

            var parms = GetFunctionParameters(ft);
            var parmString = string.Join(", ", parms);
            var annString = string.IsNullOrEmpty(o.ReturnDocumentation) ? string.Empty : $" -> {o.ReturnDocumentation}";

            return $"{ft.Name}({parmString}){annString}";
        }

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
            var propDoc = !string.IsNullOrEmpty(prop.Documentation) ? $"\n\n{prop.Documentation}" : string.Empty;
            return $"{decTypeString}{propDoc}";
        }

        private string GetFunctionHoverString(IPythonFunctionType ft, int overloadIndex = 0) {
            var sigString = GetSignatureString(ft, overloadIndex);
            var decTypeString = ft.DeclaringType != null ? $"{ft.DeclaringType.Name}." : string.Empty;
            var funcDoc = !string.IsNullOrEmpty(ft.Documentation) ? $"\n\n{ft.Documentation}" : string.Empty;
            return $"{decTypeString}{sigString}{funcDoc}";
        }

        private IEnumerable<string> GetFunctionParameters(IPythonFunctionType ft, int overloadIndex = 0) {
            var o = ft.Overloads[overloadIndex]; // TODO: display all?
            var skip = ft.IsStatic || ft.IsUnbound ? 0 : 1;
            return o.Parameters.Skip(skip).Select(p => {
                if (!string.IsNullOrEmpty(p.DefaultValueString)) {
                    return $"{p.Name}={p.DefaultValueString}";
                }
                return p.Type.IsUnknown() ? p.Name : $"{p.Name}: {p.Type.Name}";
            });
        }
    }
}
