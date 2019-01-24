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

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Analyzer.Expressions;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Documentation;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Completion {
    internal sealed class CompletionSource {
        private readonly CompletionItemSource _itemSource;

        public CompletionSource(
            IDocumentationSource docSource,
            ServerSettings.PythonCompletionOptions completionSettings
        ) {
            _itemSource = new CompletionItemSource(docSource, completionSettings);
        }

        public async Task<CompletionResult> GetCompletionsAsync(IDocumentAnalysis analysis, SourceLocation location, CancellationToken cancellationToken = default) {
            var context = new CompletionContext(analysis, location, _itemSource);

            ExpressionLocator.FindExpression(analysis.Ast, location,
                FindExpressionOptions.Complete, out var expression, out var statement, out var scope);

            switch (expression) {
                case MemberExpression me when me.Target != null && me.DotIndex > me.StartIndex && context.Position > me.DotIndex:
                    return new CompletionResult(await ExpressionCompletion.GetCompletionsFromMembersAsync(me.Target, scope, context, cancellationToken));
                case ConstantExpression ce when ce.Value != null:
                case null when context.Ast.IsInsideComment(context.Location):
                    return CompletionResult.Empty;
            }

            switch (statement) {
                case ImportStatement import:
                    return await ImportCompletion.GetCompletionsInImportAsync(import, context, cancellationToken);
                case FromImportStatement fromImport:
                    return await ImportCompletion.GetCompletionsInFromImportAsync(fromImport, context, cancellationToken);
                case FunctionDefinition fd:
                    return FunctionDefinitionCompletion.GetCompletionsForOverride(fd, context);
                case ClassDefinition cd:
                    if (!ClassDefinitionCompletion.NoCompletions(cd, context, out var addMetadataArg)) {
                        var result = await TopLevelCompletion.GetCompletionsAsync(statement, context, cancellationToken);
                        return addMetadataArg
                            ? new CompletionResult(result.Completions.Append(CompletionItemSource.MetadataArg), result.ApplicableSpan)
                            : result;
                    }
                    return null;
                case ForStatement forStatement when ForCompletion.TryGetCompletions(forStatement, context, out var result):
                    return result;
                case WithStatement withStatement when WithCompletion.TryGetCompletions(withStatement, context, out var result):
                    return result;
                case RaiseStatement raiseStatement when RaiseCompletion.TryGetCompletions(raiseStatement, context, out var result):
                    return result;
                case TryStatementHandler tryStatement when ExceptCompletion.TryGetCompletions(tryStatement, context, out var result):
                    return result;
                default: {
                        var result = await PartialExpressionCompletion.GetCompletionsAsync(scope, statement, expression, context, cancellationToken);
                        return result == CompletionResult.Empty
                            ? await TopLevelCompletion.GetCompletionsAsync(statement, context, cancellationToken)
                            : result;
                    }
            }
        }
    }
}
