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
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Analyzer.Expressions;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Completion;
using Microsoft.Python.LanguageServer.Protocol;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.LanguageServer.Sources {
    /// <summary>
    /// Implements location of symbol declaration, such as 'B' in 'from A import B'
    /// statement in the file. For 'goto definition' behavior see <see cref="DefinitionSource"/>.
    /// </summary>
    internal sealed class DeclarationSource : DefinitionSourceBase {
        public DeclarationSource(IServiceContainer services) : base(services) { }
        protected override ILocatedMember GetDefiningMember(IMember m) => m as ILocatedMember;
    }

    /// <summary>
    /// Implements location of symbol definition. For example, in 'from A import B'
    /// locates actual code of 'B' in module A. For 'goto declaration' behavior
    /// see <see cref="DeclarationSource"/>.
    /// </summary>
    internal sealed class DefinitionSource : DefinitionSourceBase {
        public DefinitionSource(IServiceContainer services) : base(services) { }
        protected override ILocatedMember GetDefiningMember(IMember m) => (m as ILocatedMember)?.GetRootDefinition();
    }

    internal abstract class DefinitionSourceBase {
        private readonly IServiceContainer _services;

        protected DefinitionSourceBase(IServiceContainer services) {
            _services = services;
        }

        protected abstract ILocatedMember GetDefiningMember(IMember m);

        /// <summary>
        /// Locates definition or declaration of a symbol at the provided location.
        /// </summary>
        /// <param name="analysis">Document analysis.</param>
        /// <param name="location">Location in the document.</param>
        /// <param name="definingMember">Member location or null of not found.</param>
        /// <returns>Definition location (module URI and the text range).</returns>
        public Reference FindDefinition(IDocumentAnalysis analysis, SourceLocation location, out ILocatedMember definingMember) {
            definingMember = null;
            if (analysis?.Ast == null) {
                return null;
            }

            ExpressionLocator.FindExpression(analysis.Ast, location,
                FindExpressionOptions.Hover, out var exprNode, out var statement, out var exprScope);

            if (exprNode is ConstantExpression || !(exprNode is Expression expr)) {
                return null; // No goto definition for literals.
            }

            Reference reference = null;
            switch (statement) {
                // Check if this is a relative import
                case FromImportStatement fromImport:
                    reference = HandleFromImport(analysis, location, fromImport, exprNode, out definingMember);
                    break;
                case ImportStatement import:
                    reference = HandleImport(analysis, import, exprNode, out definingMember);
                    break;
            }

            if (reference != null) {
                return reference.uri == null ? null : reference;
            }

            var eval = analysis.ExpressionEvaluator;
            using (eval.OpenScope(analysis.Document, exprScope)) {
                if (expr is MemberExpression mex) {
                    return FromMemberExpression(mex, analysis, out definingMember);
                }

                // Try variables
                var name = (expr as NameExpression)?.Name;
                if (!string.IsNullOrEmpty(name)) {
                    reference = TryFromVariable(name, analysis, location, statement, out definingMember);
                }
            }

            return reference;
        }

        private Reference HandleFromImport(IDocumentAnalysis analysis, SourceLocation location, FromImportStatement statement, Node expr, out ILocatedMember definingMember) {
            definingMember = null;

            // Are in the dotted name?
            var locationIndex = location.ToIndex(analysis.Ast);
            if (statement.Root.StartIndex <= locationIndex && locationIndex <= statement.Root.EndIndex) {
                var mres = analysis.Document.Interpreter.ModuleResolution;
                var imports = mres.CurrentPathResolver.FindImports(analysis.Document.FilePath, statement);
                IPythonModule module = null;
                switch (imports) {
                    case ModuleImport moduleImport:
                        module = mres.GetImportedModule(moduleImport.FullName);
                        break;
                    case ImplicitPackageImport packageImport:
                        module = mres.GetImportedModule(packageImport.FullName);
                        break;
                }

                definingMember = module;
                return module != null
                    ? new Reference { range = default, uri = CanNavigateToModule(module) ? module.Uri : null }
                    : null;
            }

            // We are in what/as part
            var nex = expr as NameExpression;
            var name = nex?.Name;
            if (string.IsNullOrEmpty(name)) {
                return null;
            }

            // From X import A
            var value = analysis.ExpressionEvaluator.GetValueFromExpression(nex);
            if (value.IsUnknown()) {
                // From X import A as B
                var index = statement.Names.IndexOf(x => x?.Name == name);
                if (index >= 0 && index < statement.AsNames.Count) {
                    value = analysis.ExpressionEvaluator.GetValueFromExpression(statement.AsNames[index]);
                }
            }

            if (!value.IsUnknown()) {
                definingMember = value as ILocatedMember;
                return FromMember(definingMember);
            }

            return null;
        }

        private Reference HandleImport(IDocumentAnalysis analysis, ImportStatement statement, Node expr, out ILocatedMember definingMember) {
            definingMember = null;

            var name = (expr as NameExpression)?.Name;
            if (string.IsNullOrEmpty(name)) {
                return null;
            }

            var index = statement.Names.IndexOf(x => x?.MakeString() == name);
            if (index < 0) {
                return null;
            }

            var module = analysis.Document.Interpreter.ModuleResolution.GetImportedModule(name);
            if (module != null) {
                definingMember = module;
                return new Reference { range = default, uri = CanNavigateToModule(module) ? module.Uri : null };
            }

            // Import A as B
            if (index >= 0 && index < statement.AsNames.Count) {
                var value = analysis.ExpressionEvaluator.GetValueFromExpression(statement.AsNames[index]);
                if (!value.IsUnknown()) {
                    definingMember = value as ILocatedMember;
                    return FromMember(definingMember);
                }
            }
            return null;
        }


        private Reference TryFromVariable(string name, IDocumentAnalysis analysis, SourceLocation location, Node statement, out ILocatedMember definingMember) {
            definingMember = null;

            var m = analysis.ExpressionEvaluator.LookupNameInScopes(name, out var scope);
            if (m == null || !(scope.Variables[name] is IVariable v)) {
                return null;
            }

            definingMember = v;
            if (statement is ImportStatement || statement is FromImportStatement) {
                // If we are on the variable definition in this module,
                // then goto declaration should go to the parent, if any.
                // Goto to definition navigates to the very root of the parent chain.
                var indexSpan = v.Definition.Span.ToIndexSpan(analysis.Ast);
                var index = location.ToIndex(analysis.Ast);
                if (indexSpan.Start <= index && index < indexSpan.End) {
                    var parent = GetDefiningMember((v as IImportedMember)?.Parent);
                    var definition = parent?.Definition ?? (v.Value as ILocatedMember)?.Definition;
                    if (definition != null && CanNavigateToModule(definition.DocumentUri)) {
                        return new Reference { range = definition.Span, uri = definition.DocumentUri };
                    }
                }
            }
            return FromMember(v);
        }

        private Reference FromMemberExpression(MemberExpression mex, IDocumentAnalysis analysis, out ILocatedMember definingMember) {
            definingMember = null;

            var eval = analysis.ExpressionEvaluator;
            var target = eval.GetValueFromExpression(mex.Target);
            var type = target?.GetPythonType();

            switch (type) {
                case IPythonModule m when m.Analysis.GlobalScope != null:
                    // Module GetMember returns module variable value while we
                    // want the variable itself since we want to know its location.
                    var v1 = m.Analysis.GlobalScope.Variables[mex.Name];
                    if (v1 != null) {
                        definingMember = v1;
                        return FromMember(v1);
                    }
                    break;

                case IPythonClassType cls:
                    // Data members may be instances that are not tracking locations.
                    // In this case we'll try look up the respective variable instead.
                    using (eval.OpenScope(cls)) {
                        eval.LookupNameInScopes(mex.Name, out _, out var v2, LookupOptions.Local);
                        if (v2 != null) {
                            definingMember = v2;
                            return FromMember(v2);
                        }
                    }
                    break;
            }

            if (type?.GetMember(mex.Name) is ILocatedMember lm) {
                definingMember = lm;
                return FromMember(lm);
            }

            return null;
        }

        private Reference FromMember(IMember m) {
            var definition = GetDefiningMember(m)?.Definition;
            var moduleUri = definition?.DocumentUri;
            // Make sure module we are looking for is not a stub
            if (m is IPythonType t) {
                moduleUri = t.DeclaringModule.ModuleType == ModuleType.Stub
                    ? t.DeclaringModule.PrimaryModule.Uri
                    : t.DeclaringModule.Uri;
            }
            if (definition != null && CanNavigateToModule(moduleUri)) {
                return new Reference { range = definition.Span, uri = moduleUri };
            }
            return null;
        }

        private bool CanNavigateToModule(Uri uri) {
            if (uri == null) {
                return false;
            }
            var rdt = _services.GetService<IRunningDocumentTable>();
            var doc = rdt.GetDocument(uri);
            return CanNavigateToModule(doc);
        }

        private static bool CanNavigateToModule(IPythonModule m)
            => m?.ModuleType == ModuleType.User ||
               m?.ModuleType == ModuleType.Package ||
               m?.ModuleType == ModuleType.Library;
    }
}
