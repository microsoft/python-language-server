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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Documentation;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Completion {
    internal sealed class CompletionSource {
        private readonly CompletionContext _context;
        private readonly ScopeStatement _scope;
        private readonly Node _statement;
        private readonly Node _expression;

        public CompletionSource(
            IDocumentAnalysis analysis,
            SourceLocation location,
            IDocumentationSource docSource,
            ServerSettings.PythonCompletionOptions completionSettings
        ) {
            var itemSource = new CompletionItemSource(docSource, completionSettings);
            _context = new CompletionContext(analysis, location, itemSource);

            ExpressionLocator.FindExpression(analysis.Ast, location, out var expression, out var statement, out var scope);
            _scope = scope;
            _statement = statement;
            _expression = expression;
        }

        public async Task<CompletionResult> GetCompletionsAsync(CancellationToken cancellationToken = default) {
            switch (_expression) {
                case MemberExpression me when me.Target != null && me.DotIndex > me.StartIndex && _context.Position > me.DotIndex:
                    return new CompletionResult(await ExpressionCompletion.GetCompletionsFromMembersAsync(me.Target, _scope, _context, cancellationToken));
                case ConstantExpression ce when ce.Value != null:
                case null when _context.Ast.IsInsideComment(_context.Location):
                    return CompletionResult.Empty;
            }

            switch (_statement) {
                case ImportStatement import:
                    return await ImportCompletion.GetCompletionsInImportAsync(import, _context, cancellationToken);
                case FromImportStatement fromImport:
                    return await ImportCompletion.GetCompletionsInFromImportAsync(fromImport, _context, cancellationToken);
                case FunctionDefinition fd:
                    return FunctionDefinitionCompletion.GetCompletionsForOverride(fd, _context);
                case ClassDefinition cd:
                    if (!ClassDefinitionCompletion.NoCompletions(cd, _context, out var addMetadataArg)) {
                        var result = await TopLevelCompletion.GetCompletionsAsync(_statement, _context, cancellationToken);
                        return addMetadataArg
                            ? new CompletionResult(result.Completions.Append(CompletionItemSource.MetadataArg), result.ApplicableSpan)
                            : result;
                    }
                    return null;
                case ForStatement forStatement when ForCompletion.TryGetCompletions(forStatement, _context, out var result):
                    return result;
                case WithStatement withStatement when WithCompletion.TryGetCompletions(withStatement, _context, out var result):
                    return result;
                case RaiseStatement raiseStatement when RaiseCompletion.TryGetCompletions(raiseStatement, _context, out var result):
                    return result;
                case TryStatementHandler tryStatement when ExceptCompletion.TryGetCompletions(tryStatement, _context, out var result):
                    return result;
                default: {
                        var result = await PartialExpressionCompletion.GetCompletionsAsync(_scope, _statement, _expression, _context, cancellationToken);
                        return result == CompletionResult.Empty
                            ? await TopLevelCompletion.GetCompletionsAsync(_statement, _context, cancellationToken)
                            : result;
                    }
            }
        }
    }
}
