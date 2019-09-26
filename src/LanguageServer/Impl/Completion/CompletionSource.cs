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
using Microsoft.Python.Analysis.Analyzer.Expressions;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Completion {
    internal sealed class CompletionSource {
        private readonly CompletionItemSource _itemSource;
        private readonly IServiceContainer _services;

        public CompletionSource(IDocumentationSource docSource, ServerSettings.PythonCompletionOptions completionSettings, IServiceContainer services) {
            _itemSource = new CompletionItemSource(docSource, completionSettings);
            _services = services;
        }

        public ServerSettings.PythonCompletionOptions Options {
            get => _itemSource.Options;
            set => _itemSource.Options = value;
        }

        public CompletionResult GetCompletions(IDocumentAnalysis analysis, SourceLocation location) {
            if(analysis.Document.ModuleType != ModuleType.User) {
                return CompletionResult.Empty;
            }

            var context = new CompletionContext(analysis, location, _itemSource, _services);

            ExpressionLocator.FindExpression(analysis.Ast, location,
                FindExpressionOptions.Complete, out var expression, out var statement, out var scope);

            switch (expression) {
                case MemberExpression me when me.Target != null && me.DotIndex > me.StartIndex && context.Position > me.DotIndex:
                    return new CompletionResult(ExpressionCompletion.GetCompletionsFromMembers(me.Target, scope, context));
                case ConstantExpression ce1 when ce1.Value is double || ce1.Value is float:
                    // no completions on integer ., the user is typing a float
                    return CompletionResult.Empty;
                case ConstantExpression ce2 when ce2.Value is string:
                case ConstantExpression ce3 when ce3.Value is AsciiString:
                // no completions in strings
                case ConstantExpression ce4 when ce4.Value is Ellipsis:
                // no completions in ellipsis
                case null when context.Ast.IsInsideComment(context.Location):
                case null when context.Ast.IsInsideString(context.Location):
                    return CompletionResult.Empty;
            }

            if (statement is ImportStatement import) {
                var result = ImportCompletion.TryGetCompletions(import, context);
                if (result != null) {
                    return result;
                }
            }
            if (statement is FromImportStatement fromImport) {
                var result = ImportCompletion.GetCompletionsInFromImport(fromImport, context);
                if (result != null) {
                    return result;
                }
            }

            switch (statement) {
                case FunctionDefinition fd when FunctionDefinitionCompletion.TryGetCompletionsForOverride(fd, context, null, out var result):
                    return result;
                case FunctionDefinition fd when FunctionDefinitionCompletion.NoCompletions(fd, context.Position, context.Ast):
                    return CompletionResult.Empty;
                case ClassDefinition cd:
                    if (!ClassDefinitionCompletion.NoCompletions(cd, context, out var addMetadataArg)) {
                        var result = TopLevelCompletion.GetCompletions(statement, scope, context);
                        return addMetadataArg
                            ? new CompletionResult(result.Completions.Append(CompletionItemSource.MetadataArg), result.ApplicableSpan)
                            : result;
                    }
                    return CompletionResult.Empty;
                case ForStatement forStatement when ForCompletion.TryGetCompletions(forStatement, context, out var result):
                    return result;
                case WithStatement withStatement when WithCompletion.TryGetCompletions(withStatement, context, out var result):
                    return result;
                case RaiseStatement raiseStatement when RaiseCompletion.TryGetCompletions(raiseStatement, context, out var result):
                    return result;
                case TryStatementHandler tryStatement when ExceptCompletion.TryGetCompletions(tryStatement, context, out var result):
                    return result;
                default: {
                        var result = ErrorExpressionCompletion.GetCompletions(scope, statement, expression, context);
                        if (result == null) {
                            return CompletionResult.Empty;
                        }
                        return result == CompletionResult.Empty ? TopLevelCompletion.GetCompletions(statement, scope, context) : result;
                    }
            }
        }
    }
}
