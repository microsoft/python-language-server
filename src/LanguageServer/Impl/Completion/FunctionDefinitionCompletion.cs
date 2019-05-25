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

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Completion {
    internal static class FunctionDefinitionCompletion {
        public static bool TryGetCompletionsForOverride(FunctionDefinition function, CompletionContext context, SourceLocation? location, out CompletionResult result) {
            result = CompletionResult.Empty;
            if (!string.IsNullOrEmpty(function.NameExpression?.Name) && context.Position > function.NameExpression.EndIndex) {
                return false;
            }

            if (function.Parent is ClassDefinition cd && function.NameExpression != null && context.Position > function.NameExpression.StartIndex) {
                var loc = function.GetStart(context.Ast);
                var overrideable = GetOverrideable(context, location).ToArray();
                overrideable = !string.IsNullOrEmpty(function.Name)
                        ? overrideable.Where(o => o.Name.StartsWithOrdinal(function.Name)).ToArray()
                        : overrideable;
                var items = overrideable.Select(o => ToOverrideCompletionItem(o, cd, context, new string(' ', loc.Column - 1)));
                result = new CompletionResult(items);
                return true;
            }

            return false;
        }

        private static CompletionItem ToOverrideCompletionItem(IPythonFunctionOverload o, ClassDefinition cd, CompletionContext context, string indent) {
            return new CompletionItem {
                label = o.Name,
                insertText = MakeOverrideCompletionString(indent, o, cd.Name, context),
                insertTextFormat = InsertTextFormat.PlainText,
                kind = CompletionItemKind.Method
            };
        }

        private static string MakeOverrideParameter(IParameterInfo paramInfo, string defaultValue) {
            if (paramInfo.IsParamArray) {
                return $"*{paramInfo.Name}";
            }
            if (paramInfo.IsKeywordDict) {
                return $"**{paramInfo.Name}";
            }
            return !string.IsNullOrEmpty(paramInfo.DefaultValueString) ? $"{paramInfo.Name}={defaultValue}" : paramInfo.Name;
        }

        private static string MakeOverrideCompletionString(string indentation, IPythonFunctionOverload overload, string className, CompletionContext context) {
            var sb = new StringBuilder();

            var first = overload.Parameters.FirstOrDefault();
            var fn = overload.ClassMember as IPythonFunctionType;
            var skipFirstParameters = fn?.IsStatic == true ? overload.Parameters : overload.Parameters.Skip(1);

            sb.AppendLine(overload.Name + "(" + string.Join(", ", overload.Parameters.Select(p => MakeOverrideParameter(p, p.DefaultValueString))) + "):");
            sb.Append(indentation);

            if (overload.Parameters.Count > 0) {
                var parameterString = string.Join(", ", skipFirstParameters.Select(p => MakeOverrideParameter(p, p.Name)));

                if (context.Ast.LanguageVersion.Is3x()) {
                    sb.AppendFormat("return super().{0}({1})",
                        overload.Name,
                        parameterString);
                } else if (!string.IsNullOrEmpty(className)) {
                    sb.AppendFormat("return super({0}, {1}).{2}({3})",
                        className,
                        first?.Name ?? string.Empty,
                        overload.Name,
                        parameterString);
                } else {
                    sb.Append("pass");
                }
            } else {
                sb.Append("pass");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Gets information about methods defined on base classes but not directly on the current class.
        /// </summary>
        private static IEnumerable<IPythonFunctionOverload> GetOverrideable(CompletionContext context, SourceLocation? location = null) {
            var result = new List<IPythonFunctionOverload>();

            var scope = context.Analysis.FindScope(location ?? context.Location);
            if (!(scope?.Node is ClassDefinition cls)) {
                return result;
            }
            var handled = new HashSet<string>(scope.Children.Select(child => child.Name));

            var classVar = scope.OuterScope.Variables[cls.Name];
            var classType = classVar?.GetValue<IMember>()?.GetPythonType<IPythonClassType>();
            if (classType?.Mro == null) {
                return result;
            }

            foreach (var baseClassType in classType.Mro.Skip(1).OfType<IPythonClassType>()) {

                var functions = baseClassType.GetMemberNames().Select(n => baseClassType.GetMember(n)).OfType<IPythonFunctionType>();
                foreach (var f in functions.Where(f => f.Overloads.Count > 0)) {
                    var overload = f.Overloads.Aggregate(
                        (best, o) => o.Parameters.Count > best.Parameters.Count ? o : best
                    );

                    if (handled.Contains(overload.Name)) {
                        continue;
                    }

                    handled.Add(overload.Name);
                    result.Add(overload);
                }
            }

            return result;
        }

        public static bool NoCompletions(FunctionDefinition fd, int position, PythonAst ast) {
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

            if (fd.NameExpression != null) {
                if (fd.NameExpression.StartIndex > fd.KeywordEndIndex && position >= fd.NameExpression.StartIndex) {
                    return true;
                }

                var defLine = ast.IndexToLocation(fd.KeywordEndIndex).Line;
                var posLine = ast.IndexToLocation(position).Line;
                if (fd.NameExpression.StartIndex == fd.NameExpression.EndIndex && defLine == posLine) {
                    return false;
                }
            }

            return position > fd.KeywordEndIndex;
        }
    }
}
