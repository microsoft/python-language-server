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
using System.Text;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Completion {
    internal static class FunctionDefinitionCompletion {
        public static bool NoCompletions(FunctionDefinition fd, int position) {
            // Here we work backwards through the various parts of the definitions.
            // When we find that Index is within a part, we return either the available
            // completions 
            if (fd.HeaderIndex > fd.StartIndex && position > fd.HeaderIndex) {
                return false;
            }

            if (position == fd.HeaderIndex) {
                return true;
            }

            foreach (var p in fd.Parameters.Reverse()) {
                if (position >= p.StartIndex) {
                    if (p.Annotation != null) {
                        return position < p.Annotation.StartIndex;
                    }

                    if (p.DefaultValue != null) {
                        return position < p.DefaultValue.StartIndex;
                    }
                }
            }

            if (fd.NameExpression != null && fd.NameExpression.StartIndex > fd.KeywordEndIndex && position >= fd.NameExpression.StartIndex) {
                return true;
            }

            return position > fd.KeywordEndIndex;
        }

        public static bool TryGetCompletionsForOverride(FunctionDefinition function, CompletionContext context, out CompletionResult result) {
            if (function.Parent is ClassDefinition cd && string.IsNullOrEmpty(function.Name) && function.NameExpression != null && context.Position > function.NameExpression.StartIndex) {
                var loc = function.GetStart(context.Ast);
                result = Analysis.GetOverrideable(loc).Select(o => ToOverrideCompletionItem(o, cd, new string(' ', loc.Column - 1)));
                return true;
            }

            result = null;
            return false;
        }

        private CompletionItem ToOverrideCompletionItem(IOverloadResult o, ClassDefinition cd, string indent) {
            return new CompletionItem {
                label = o.Name,
                insertText = MakeOverrideCompletionString(indent, o, cd.Name),
                insertTextFormat = InsertTextFormat.PlainText,
                kind = CompletionItemKind.Method
            };
        }

        private static string MakeOverrideDefParameter(IParameterInfo paramInfo) 
            => !string.IsNullOrEmpty(paramInfo.DefaultValueString) ? $"{paramInfo.Name}={paramInfo.DefaultValueString}" : paramInfo.Name;

        private static string MakeOverrideCallParameter(IParameterInfo paramInfo) {
            if (paramInfo.Name.StartsWithOrdinal("*")) {
                return paramInfo.Name;
            }
            return MakeOverrideDefParameter(paramInfo);
        }

        private string MakeOverrideCompletionString(string indentation, IPythonFunctionOverload overload, string className) {
            var sb = new StringBuilder();

            IParameterInfo first;
            IParameterInfo[] skipFirstParameters;
            IParameterInfo[] allParameters;

            sb.AppendLine(overload.Name + "(" + string.Join(", ", overload.Parameters.Select(MakeOverrideDefParameter)) + "):");
            sb.Append(indentation);

            if (overload.Parameters.Count > 0) {
                var parameterString = string.Join(", ", skipFirstParameters.Select(MakeOverrideCallParameter));

                if (context.Ast.LanguageVersion.Is3x()) {
                    sb.AppendFormat("return super().{0}({1})",
                        result.Name,
                        parameterString);
                } else if (!string.IsNullOrEmpty(className)) {
                    sb.AppendFormat("return super({0}, {1}).{2}({3})",
                        className,
                        first?.Name ?? string.Empty,
                        result.Name,
                        parameterString);
                } else {
                    sb.Append("pass");
                }
            } else {
                sb.Append("pass");
            }

            return sb.ToString();
        }
    }
}
