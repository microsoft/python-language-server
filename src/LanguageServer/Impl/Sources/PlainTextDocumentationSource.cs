﻿// Copyright(c) Microsoft Corporation
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

using System.Collections.Generic;
using System.Linq;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.LanguageServer.Protocol;

namespace Microsoft.Python.LanguageServer.Sources {
    internal sealed class PlainTextDocumentationSource : IDocumentationSource {
        public InsertTextFormat DocumentationFormat => InsertTextFormat.PlainText;

        public MarkupContent GetTypeHover(string name, IPythonType type) {
            string text = name;
            if (!type.IsUnknown()) {
                switch (type) {
                    case IPythonFunctionType ft:
                        text = GetFunctionHoverString(ft);
                        break;
                    case IPythonClassType cls: {
                        var clsDoc = !string.IsNullOrEmpty(cls.Documentation) ? $"\n\n{cls.Documentation}" : string.Empty;
                        text = string.IsNullOrEmpty(name) ? $"class {cls.Name}{clsDoc}" : $"{name}: {cls.Name}";
                        break;
                    }
                    default: {
                        var typeDoc = !string.IsNullOrEmpty(type.Documentation) ? $"\n\n{type.Documentation}" : string.Empty;
                        text = !string.IsNullOrEmpty(name) ? $"{name}: {type.Name}{typeDoc}" : $"{type.Name}{typeDoc}";
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

        private string GetFunctionHoverString(IPythonFunctionType ft, int overloadIndex = 0) {
            var sigString = GetSignatureString(ft, overloadIndex);
            var funcDoc = !string.IsNullOrEmpty(ft.Documentation) ? $"\n\n{ft.Documentation}" : string.Empty;
            return $"{sigString}{funcDoc}";
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
