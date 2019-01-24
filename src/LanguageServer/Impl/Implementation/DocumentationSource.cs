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
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.LanguageServer.Documentation;
using Microsoft.Python.LanguageServer.Protocol;

namespace Microsoft.Python.LanguageServer.Implementation {
    internal sealed class PlainTextDocSource : IDocumentationSource {
        public InsertTextFormat DocumentationFormat => InsertTextFormat.PlainText;

        public MarkupContent GetDocumentation(string name, IPythonType type) {
            string text;
            switch (type) {
                case IPythonFunctionType ft: {
                        var o = ft.Overloads.First();
                        var declType = ft.DeclaringType != null ? $"{ft.DeclaringType.Name}." : string.Empty;
                        var skip = string.IsNullOrEmpty(declType) ? 0 : 1;
                        var parms = o.Parameters.Skip(skip).Select(p => string.IsNullOrEmpty(p.DefaultValueString) ? p.Name : $"{p.Name}={p.DefaultValueString}");
                        var parmString = string.Join(", ", parms);
                        var annString = string.IsNullOrEmpty(o.ReturnDocumentation) ? string.Empty : $" -> {o.ReturnDocumentation}";
                        var funcDoc = !string.IsNullOrEmpty(ft.Documentation) ? $"\n\n{ft.Documentation}" : string.Empty;
                        text = $"{declType}{ft.Name}({parmString}){annString}{funcDoc}";
                    }
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
            return new MarkupContent { kind = MarkupKind.PlainText, value = text };
        }
    }
}
