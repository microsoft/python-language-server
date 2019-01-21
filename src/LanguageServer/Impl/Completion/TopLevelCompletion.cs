using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Python.Analysis.Analyzer.Expressions;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Completion {
    internal static class TopLevelCompletion {
        public static void GetCompletions(CompletionContext context, out CompletionResult result) {
            result = CompletionResult.Empty;
            SourceSpan? applicableSpan = null;

            var options = Options | GetMemberOptions.ForEval | GetMemberOptionsForTopLevelCompletions(Statement, context.Position, out var span);
            if (span.HasValue) {
                applicableSpan = new SourceSpan(context.IndexToLocation(span.Value.Start), context.IndexToLocation(span.Value.End));
            }

            var members = Analysis.GetAllMembers(context.Position, options);

            var finder = new ExpressionFinder(Tree, new GetExpressionOptions { Calls = true });
            if (finder.GetExpression(context.Position) is CallExpression callExpr && callExpr.GetArgumentAtIndex(context.Ast, context.Position, out _)) {
                var argNames = Analysis.GetSignatures(callExpr.Target, context.Position)
                    .SelectMany(o => o.Parameters).Select(p => p?.Name)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Distinct()
                    .Except(callExpr.Args.MaybeEnumerate().Select(a => a.Name).Where(n => !string.IsNullOrEmpty(n)))
                    .Select(n => new MemberResult($"{n}=", PythonMemberType.NamedArgument) as IMemberResult)
                    .ToArray();

                members = members.Concat(argNames);
            }

            return members
                .Where(m => !string.IsNullOrEmpty(m.Completion) || !string.IsNullOrEmpty(m.Name))
                .Select(ToCompletionItem);
        }
    }
}
