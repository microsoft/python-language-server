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
using System.Diagnostics;
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
using Microsoft.Python.Parsing.Extensions;

namespace Microsoft.Python.Analysis.Linting.UndefinedVariables {
    internal sealed class UnusedImportsLinter : ILinter {
        public IReadOnlyList<DiagnosticsEntry> Lint(IDocumentAnalysis analysis, IServiceContainer services) {
            var allVariables = new HashSet<string>(analysis.GlobalScope.GetAllVariablesBestEffort());

            var results = new List<Info>();
            foreach (var scope in GetAllScopes(analysis, CancellationToken.None)) {
                CollectUnusedImportForScope(analysis, scope, allVariables, results);
            }

            return CreateDiagnostics(results);
        }

        private IEnumerable<IScope> GetAllScopes(IDocumentAnalysis analysis, CancellationToken cancellationToken) {
            foreach (var scope in analysis.Ast.ChildNodesDepthFirst().OfType<ScopeStatement>()) {
                cancellationToken.ThrowIfCancellationRequested();

                yield return analysis.FindScope(scope.Body.GetStart(analysis.Ast));
            }
        }

        private static void CollectUnusedImportForScope(IDocumentAnalysis analysis, IScope scope, HashSet<string> allVariables, List<Info> results) {
            // * NOTE * variable declared in imported is different than same variable referenced in the code.
            //          that is because that variable is re-declared in another variable collection
            var imported = scope.Imported;
            var variableDeclared = scope.Variables;
            foreach (var name in imported.Names) {
                if (!imported.TryGetVariable(name, out var variableFromImported)) {
                    continue;
                }

                // all imported variable must be declared in this file
                Debug.Assert(variableFromImported.Definition.DocumentUri == analysis.Document.Uri);

                // all references of the imported variable must exist in this module
                Debug.Assert(variableFromImported.References.All(r => r.DocumentUri == analysis.Document.Uri));

                // skip any name that is dotted name
                // we only care about imported variable that are added to current module, not
                // one that added to other module
                // ex) import os.path
                // we care about "os", but not "os.path"
                if (name.IndexOf(".") >= 0) {
                    continue;
                }

                // name appeared in "__all__" is considered used.
                if (allVariables.Contains(name)) {
                    continue;
                }

                // we have variable from import statement, but we don't have any variable declared from actual
                // usage. meaning the import is not used.
                if (!variableDeclared.TryGetVariable(name, out var variableFromVariables)) {
                    ReportUnusedImports(variableFromImported, results, CancellationToken.None);
                    continue;
                }

                // * NOTE * this seems won't work if variable with same name declared multiple times?
                if (!LocationInfo.FullComparer.Equals(variableFromVariables.Definition, variableFromImported.Definition)) {
                    continue;
                }

                // find any reference in current file which is not the import variable definition itself
                // varaibleFromVariables reference contains references in the module and variableFromImported reference contains same imported member used in imports.
                // make sure varaibleFromVariables reference contains any reference that are not part of imports
                var usageReferences = variableFromVariables.References.Where(l => !variableFromImported.References.Any(r => LocationInfo.FullComparer.Equals(l, r))).ToArray();
                if (usageReferences.Length > 0) {
                    continue;
                }

                ReportUnusedImports(variableFromImported, results, CancellationToken.None);
            }
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
            var statement = imports.Key;
            if (statement == null) {
                // can't determine whether we need to merge or not
                return false;
            }

            // if every names in import statement has a diagnostic, then we can remove whole statement
            var names = GetNames(statement);
            return names.Count() == imports.Count();
        }

        private static void ReportUnusedImports(IVariable variable, List<Info> results, CancellationToken cancellationToken) {
            foreach (var reference in variable.References) {
                ReportUnusedImports(variable, reference, results, cancellationToken);
            }
        }

        private static void ReportUnusedImports(IVariable variable, LocationInfo reference, List<Info> results, CancellationToken cancellationToken) {
            var (span, ast, import) = GetDiagnosticSpan(variable, reference, cancellationToken);
            results.Add(new Info(
                variable.Value.MemberType,
                variable.Name,
                span,
                ast,
                import));
        }

        private static IEnumerable<(IndexSpan? nameSpan, IndexSpan? asNameSpan)> GetNames(Statement statement) {
            switch (statement) {
                case ImportStatement import:
                    return import.Names.Select(n => n?.IndexSpan).Zip(import.AsNames.Select(n => n?.IndexSpan), (i, i2) => (i, i2));
                case FromImportStatement fromImport:
                    return fromImport.Names.Select(n => n?.IndexSpan).Zip(fromImport.AsNames.Select(n => n?.IndexSpan), (i, i2) => (i, i2));
                default:
                    throw new InvalidOperationException($"unexpected statement: {statement}");
            }
        }

        private static (IndexSpan, PythonAst, Statement) GetDiagnosticSpan(IVariable variable, LocationInfo reference, CancellationToken cancellationToken) {
            // this module is this file. Ast must exist
            var ast = variable.Location.Module.Analysis.Ast;
            Debug.Assert(ast != null);

            // use some heuristic on where to show diagnostics
            var span = reference.Span.ToIndexSpan(ast);

            // see whether import statement contains just 1 symbol or multiple one
            var finder = new ExpressionFinder(ast, new FindExpressionOptions() { ImportNames = true, ImportAsNames = true, Names = true });
            var identifier = finder.GetExpression(span.Start, span.End);
            if (identifier == null) {
                return (span, ast, null);
            }

            var statement = GetAncestorsOrThis(ast.Body, identifier, cancellationToken).FirstOrDefault(c => c is ImportStatement || c is FromImportStatement) as Statement;
            switch (statement) {
                case ImportStatement _:
                case FromImportStatement _:
                    return (GetNameSpan(span, statement), ast, statement);
                default:
                    return (span, ast, null);
            }
        }

        private static IndexSpan GetNameSpan(IndexSpan definitionSpan, Statement statement) {
            var names = GetNames(statement);
            var match = names.FirstOrDefault(z => Contains(z.nameSpan, definitionSpan) || Contains(z.asNameSpan, definitionSpan));
            if (match.nameSpan == null) {
                return definitionSpan;
            }

            return IndexSpan.FromBounds(match.nameSpan.Value.Start, match.asNameSpan?.End ?? match.nameSpan.Value.End);

            bool Contains(IndexSpan? span1, IndexSpan span2) {
                return span1?.Contains(span2) ?? false;
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
            public readonly IndexSpan Span;
            public readonly PythonAst Ast;
            public readonly Statement ImportStatement;

            public Info(PythonMemberType type, string name, IndexSpan span, PythonAst ast, Statement importStatement) {
                Type = type;
                Name = name;
                Span = span;
                Ast = ast;
                ImportStatement = importStatement;
            }

            public DiagnosticsEntry ToDiagnostic() {
                return ToDiagnostic(Resources._0_1_is_declared_but_it_is_never_used_within_the_current_file.FormatUI(Type, Name), Span.ToSourceSpan(Ast));
            }

            public static DiagnosticsEntry ToDiagnostic(IEnumerable<Info> info) {
                var first = info.First();
                var span = first.ImportStatement.GetSpan(first.Ast);

                var message = info.Count() == 1 ?
                    Resources._0_1_is_declared_but_it_is_never_used_within_the_current_file.FormatUI(first.Type, first.Name) :
                    Resources._0_1_are_declared_but_they_are_never_used_within_the_current_file.FormatUI(first.Type, first.Name);

                return ToDiagnostic(message, span);
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
