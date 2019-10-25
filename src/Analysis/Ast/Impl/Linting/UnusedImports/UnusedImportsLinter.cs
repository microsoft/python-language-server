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
using System.Threading;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Analyzer.Expressions;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Linting.UndefinedVariables {
    internal sealed class UnusedImportsLinter : ILinter {
        public IReadOnlyList<DiagnosticsEntry> Lint(IDocumentAnalysis analysis, IServiceContainer services) {
            var results = new List<Info>();

            var imported = analysis.GlobalScope.Imported;
            var allVariables = new HashSet<string>(analysis.GlobalScope.GetAllVariablesBestEffort());

            // * NOTE * variable declared in imported is different than same variable referenced in the code.
            //          that is because that variable is re-declared in another variable collection
            //          not sure whether it is a bug or intentional behavior. might be intentional to distinguish
            //          same name used for 2 different varialbes. need to check
            var variableDeclared = analysis.GlobalScope.Variables;
            foreach (var name in imported.Names) {
                if (!imported.TryGetVariable(name, out var variableFromImportCollection)) {
                    continue;
                }

                // name appeared in __all__ in considered used.
                if (allVariables.Contains(name)) {
                    continue;
                }

                // we have variable from import statement, but we don't have any variable declared from actual
                // usage. meaning the import is not used.
                if (!variableDeclared.TryGetVariable(name, out var variableFromVariableCollection)) {
                    ReportUnusedImports(variableFromImportCollection, results, CancellationToken.None);
                    continue;
                }

                // * NOTE * this seems won't work if variable with same name declared multiple times?
                if (!LocationInfo.FullComparer.Equals(variableFromVariableCollection.Definition, variableFromImportCollection.Definition)) {
                    continue;
                }

                // find any reference in current file which is not the import variable definition itself
                // we need to use one from variable declared collection since FAR info is only there
                if (variableFromVariableCollection.References.Any(r => r.DocumentUri == variableFromImportCollection.Definition.DocumentUri &&
                                                                       r.Span != variableFromImportCollection.Definition.Span)) {
                    continue;
                }

                ReportUnusedImports(variableFromImportCollection, results, CancellationToken.None);
            }

            return CreateDiagnostics(results);
        }

        private IReadOnlyList<DiagnosticsEntry> CreateDiagnostics(List<Info> info) {
            if (info.Count == 0) {
                return Array.Empty<DiagnosticsEntry>();
            }

            var results = new List<DiagnosticsEntry>();
            var groupByImportStatement = info.GroupBy(i => i.ImportStatement);

            foreach (var imports in groupByImportStatement) {
                if (ShouldMerge(imports)) {
                    results.Add(Info.ToDiagnostic(imports));
                } else {
                    foreach (var import in imports) {
                        results.Add(import.ToDiagnostic());
                    }
                }
            }

            return results;
        }

        private bool ShouldMerge(IGrouping<Statement, Info> imports) {
            var first = imports.First();

            if (imports.Count() == 1) {
                if (first.ImportStatement == null) {
                    // can't determine whether we need to merge or not
                    return false;
                }

                var names = GetNames(first.ImportStatement);
                return names.Count() == 1;
            }

            return false;
        }

        private static void ReportUnusedImports(IVariable variable, List<Info> results, CancellationToken cancellationToken) {
            var (span, ast, import) = GetDiagnosticSpan(variable, cancellationToken);

            results.Add(new Info(
                variable.Value.MemberType,
                variable.Name,
                span,
                ast,
                import));
        }

        private static IEnumerable<(IndexSpan?, IndexSpan?)> GetNames(Statement statement) {
            switch (statement) {
                case ImportStatement import:
                    return import.Names.Select(n => n?.IndexSpan).Zip(import.AsNames.Select(n => n?.IndexSpan), (i, i2) => (i, i2));
                case FromImportStatement fromImport:
                    return fromImport.Names.Select(n => n?.IndexSpan).Zip(fromImport.AsNames.Select(n => n?.IndexSpan), (i, i2) => (i, i2));
                default:
                    throw new InvalidOperationException($"unexpected statement: {statement}");
            }
        }

        private static (SourceSpan, PythonAst, Statement) GetDiagnosticSpan(IVariable variable, CancellationToken cancellationToken) {
            // use some heuristic on where to show diagnostics
            var definitionSpan = variable.Definition.Span;
            var ast = variable.Location.Module.Analysis.Ast ?? variable.Location.Module.GetAst();
            if (ast == null) {
                return (definitionSpan, null, null);
            }

            // see whether import statement contains just 1 symbol or multiple one
            var finder = new ExpressionFinder(ast, new FindExpressionOptions() { ImportNames = true, ImportAsNames = true, Names = true });
            var identifier = finder.GetExpression(definitionSpan);
            if (identifier == null) {
                return (definitionSpan, ast, null);
            }

            var statement = GetAncestorsOrThis(ast.Body, identifier, cancellationToken).FirstOrDefault(c => c is ImportStatement || c is FromImportStatement) as Statement;
            switch (statement) {
                case ImportStatement _:
                case FromImportStatement _:
                    return (definitionSpan, ast, statement);
                default:
                    return (definitionSpan, ast, null);
            }
        }

        private static List<Node> GetAncestorsOrThis(Node root, Node node, CancellationToken cancellationToken) {
            var parentChain = new List<Node>();

            // there seems no way to go up the parent chain. always has to go down from the top
            while (root != null) {
                cancellationToken.ThrowIfCancellationRequested();

                var temp = root;
                root = null;

                // this assumes node is not overlapped and children are ordered from left to right
                // in textual position
                foreach (var current in temp.GetChildNodes()) {
                    if (!current.IndexSpan.Contains(node.IndexSpan)) {
                        continue;
                    }

                    parentChain.Add(current);
                    root = current;
                    break;
                }
            }

            return parentChain;
        }

        private struct Info {
            private readonly static DiagnosticsEntry.DiagnosticTags[] Tags = new[] { DiagnosticsEntry.DiagnosticTags.Unnecessary };

            public readonly PythonMemberType Type;
            public readonly string Name;
            public readonly SourceSpan Span;
            public readonly PythonAst Ast;
            public readonly Statement ImportStatement;

            public Info(PythonMemberType type, string name, SourceSpan span, PythonAst ast, Statement importStatement) {
                Type = type;
                Name = name;
                Span = span;
                Ast = ast;
                ImportStatement = importStatement;
            }

            public DiagnosticsEntry ToDiagnostic() {
                return ToDiagnostic(Resources._0_1_is_declared_but_it_is_never_used_within_the_current_file.FormatUI(Type, Name), Span);
            }

            public static DiagnosticsEntry ToDiagnostic(IEnumerable<Info> info) {
                var first = info.First();

                if (info.Count() == 1) {
                    return ToDiagnostic(
                        Resources._0_1_is_declared_but_it_is_never_used_within_the_current_file.FormatUI(first.Type, first.Name),
                        first.ImportStatement.GetSpan(first.Ast));
                }

                return new DiagnosticsEntry(
                    Resources._0_1_are_declared_but_they_are_never_used_within_the_current_file.FormatUI(first.Type, first.Name),
                    first.ImportStatement.GetSpan(first.Ast),
                    ErrorCodes.UnusedImport,
                    Parsing.Severity.Hint,
                    DiagnosticSource.Linter,
                    Tags);
            }

            private static DiagnosticsEntry ToDiagnostic(string message, SourceSpan span) {
                return new DiagnosticsEntry(
                    message,
                    span,
                    ErrorCodes.UnusedImport,
                    Parsing.Severity.Hint,
                    DiagnosticSource.Linter,
                    Tags);
            }
        }
    }
}
