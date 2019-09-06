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
using System.Linq;
using System.Text;
using Microsoft.Python.Analysis;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Analyzer.Expressions;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Core.Text;
using Microsoft.Python.LanguageServer.Completion;
using Microsoft.Python.LanguageServer.Documents;
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

            // Are we in the module name (i.e. A in 'from A import B')?
            var locationIndex = location.ToIndex(analysis.Ast);
            if (statement.Root.StartIndex <= locationIndex && locationIndex <= statement.Root.EndIndex) {
                definingMember = module;
                return module != null
                    ? new Reference { range = default, uri = CanNavigateToModule(module) ? module.Uri : null }
                    : null;
            }

            if (module == null) {
                return null;
            }

            // Are we in the member name part (ie. B in 'from A import B')?
            // Handle 'from A import B' similar to 'import A.B' 
            var partReference = FindModulePartReference(statement.Names, expr, module, out definingMember);
            if (partReference != null) {
                return partReference;
            }

            // Are we in 'as' names?
            var name = (expr as NameExpression)?.Name;
            if (string.IsNullOrEmpty(name)) {
                return null;
            }

            var asName = statement.AsNames.FirstOrDefault(x => x?.Name == name);
            if (asName != null) {
                var value = analysis.ExpressionEvaluator.GetValueFromExpression(asName);
                if (!value.IsUnknown()) {
                    definingMember = value as ILocatedMember;
                    return FromMember(definingMember);
                }
            }

            return null;
        }

        private static Reference FindModulePartReference(ImmutableArray<NameExpression> names, Node expr, IPythonModule module, out ILocatedMember definingMember) {
            definingMember = null;
            var part = names.FirstOrDefault(x => x.IndexSpan.Start <= expr.StartIndex && x.IndexSpan.Start <= expr.EndIndex);
            if (part != null) {
                if (module.Analysis.GlobalScope.Variables[part.Name] is ILocatedMember lm && lm.Location.Module is ILocationConverter lc) {
                    definingMember = lm;
                    return new Reference {
                        range = lm.Location.IndexSpan.ToSourceSpan(lc),
                        uri = CanNavigateToModule(module) ? module.Uri : null
                    };
                }
            }
            return null;
        }

        private Reference HandleImport(IDocumentAnalysis analysis, ImportStatement statement, Node expr, out ILocatedMember definingMember) {
            definingMember = null;

            var name = (expr as NameExpression)?.Name;
            if (string.IsNullOrEmpty(name)) {
                return null;
            }

            var reference = FindModuleNamePartReference(analysis, statement.Names, expr, out definingMember);
            if(reference != null) {
                return reference;
            }

            // Import A as B
            var asName = statement.AsNames.FirstOrDefault(n => n.IndexSpan.Start <= expr.StartIndex && n.IndexSpan.Start <= expr.EndIndex);
            if (asName != null) {
                var value = analysis.ExpressionEvaluator.GetValueFromExpression(asName);
                if (!value.IsUnknown()) {
                    definingMember = value as ILocatedMember;
                    return FromMember(definingMember);
                }
            }
            return null;
        }

        /// <summary>
        /// Given dotted name located reference to the part of the name. For example, given
        /// 'os.path' and the name expression 'path' locates definition of 'path' part of 'os' module.
        /// </summary>
        private static Reference FindModuleNamePartReference(IDocumentAnalysis analysis, ImmutableArray<ModuleName> dottedName, Node expr, out ILocatedMember definingMember) {
            definingMember = null;
            var moduleName = dottedName.FirstOrDefault(x => x.IndexSpan.Start <= expr.StartIndex && x.IndexSpan.Start <= expr.EndIndex);
            if (moduleName == null) {
                return null;
            }

            var module = analysis.Document.Interpreter.ModuleResolution.GetImportedModule(moduleName.Names.First().Name);
            foreach (var member in moduleName.Names.Skip(1)) {
                if (module == null) {
                    return null;
                }

                if (member.StartIndex >= expr.EndIndex) {
                    break;
                }

                if (member.StartIndex <= expr.EndIndex && member.EndIndex <= expr.EndIndex) {
                    if (module.Analysis.GlobalScope.Variables[member.Name] is ILocatedMember lm && lm.Location.Module is ILocationConverter lc) {
                        definingMember = lm;
                        return new Reference {
                            range = lm.Location.IndexSpan.ToSourceSpan(lc),
                            uri = CanNavigateToModule(module) ? module.Uri : null
                        };
                    }
                }
                module = module.GetMember(member.Name) as IPythonModule;
            }

            if (module != null) {
                definingMember = module;
                return new Reference { range = default, uri = CanNavigateToModule(module) ? module.Uri : null };
            }
            return null;
        }

        private Reference TryFromVariable(string name, IDocumentAnalysis analysis, SourceLocation location, Node statement, out ILocatedMember definingMember) {
            definingMember = null;

            var m = analysis.ExpressionEvaluator.LookupNameInScopes(name, out var scope, LookupOptions.All);
            if (m == null || scope.Module.ModuleType == ModuleType.Builtins || !(scope.Variables[name] is IVariable v)) {
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
                case IPythonModule m when m.GlobalScope != null:
                    // Module GetMember returns module variable value while we
                    // want the variable itself since we want to know its location.
                    var v1 = m.GlobalScope.Variables[mex.Name];
                    if (v1 != null) {
                        definingMember = v1;
                        return FromMember(v1);
                    }
                    break;

                case IPythonClassType cls:
                    // Data members may be PythonInstances which do not track their declaration location.
                    // In this case we'll try looking up the respective variable instead according to Mro.
                    foreach (var b in cls.Mro.OfType<IPythonClassType>()) {
                        using (eval.OpenScope(b)) {
                            eval.LookupNameInScopes(mex.Name, out _, out var v2, LookupOptions.Local);
                            if (v2 != null) {
                                definingMember = v2;
                                return FromMember(v2);
                            }
                        }
                    }
                    break;
            }

            // If we cannot find anything, just look up from GetMember
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
            //  Allow navigation to modules not in RDT - most probably
            // it is a module that was restored from database.
            return doc == null || CanNavigateToModule(doc);
        }

        private static bool CanNavigateToModule(IPythonModule m)
            => m?.ModuleType == ModuleType.User ||
               m?.ModuleType == ModuleType.Stub ||
               m?.ModuleType == ModuleType.Package ||
               m?.ModuleType == ModuleType.Library ||
               m?.ModuleType == ModuleType.Specialized;
    }
}
