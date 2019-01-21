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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Documentation;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Completion {
    internal sealed class CompletionSource {
        private readonly CompletionContext _context;
        private readonly Expression _expression;
        private readonly CompletionItemSource _itemSource;

        public CompletionSource(
            IDocumentAnalysis analysis,
            PythonAst ast,
            SourceLocation location,
            IDocumentationSource docSource,
            ServerSettings.PythonCompletionOptions completionSettings
        ) {
            _itemSource = new CompletionItemSource(docSource, completionSettings);
            _context = new CompletionContext(analysis, ast, location, _itemSource);

            ExpressionLocator.FindExpression(ast, location, out var expression, out var statement, out var scope);
            Scope = scope;
            Statement = statement;
            Expression = expression;
        }

        public ScopeStatement Scope { get; }
        public Node Statement { get; }
        public Node Expression { get; }

        public async Task<CompletionResult> GetCompletionsAsync(CancellationToken cancellationToken = default) {
            switch (Expression) {
                case MemberExpression me when me.Target != null && me.DotIndex > me.StartIndex && _context.Position > me.DotIndex:
                    return new CompletionResult(await GetCompletionsFromMembersAsync(me, cancellationToken));
                case ConstantExpression ce when ce.Value != null:
                case null when _context.Ast.IsInsideComment(_context.Location):
                    return CompletionResult.Empty;
            }

            switch (Statement) {
                case ImportStatement import:
                    return await ImportCompletion.GetCompletionsInImportAsync(import, _context, cancellationToken);
                case FromImportStatement fromImport:
                    return await ImportCompletion.GetCompletionsInFromImportAsync(fromImport, _context, cancellationToken);
                case FunctionDefinition fd:
                    return FunctionDefinitionCompletion.GetCompletionsForOverride(fd, _context);
                case ClassDefinition cd:
                    if (!ClassDefinitionCompletion.NoCompletions(cd, _context, out var addMetadataArg)) {
                        var result = await TopLevelCompletion.GetCompletionsAsync(Statement, _context, cancellationToken);
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
                default:
                    PartialExpressionCompletion.
                    return GetCompletionsFromError() ?? GetCompletionsFromTopLevel();
            }
        }

        private async Task<IEnumerable<CompletionItem>> GetCompletionsFromMembersAsync(MemberExpression me, CancellationToken cancellationToken = default) {
            using (_context.Analysis.ExpressionEvaluator.OpenScope(Scope)) {
                var value = await _context.Analysis.ExpressionEvaluator.GetValueFromExpressionAsync(me.Target, cancellationToken);
                if (!value.IsUnknown()) {
                    var type = value.GetPythonType();
                    var names = type.GetMemberNames().ToArray();
                    var types = names.Select(n => type.GetMember(n)).ToArray();
                    return names.Zip(types, (n, t) => _itemSource.CreateCompletionItem(n, t));
                }
            }
            return Enumerable.Empty<CompletionItem>();
        }
    }
}
