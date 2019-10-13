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
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.OS;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Dependencies {
    internal sealed class ImportExportWalker : PythonWalker {
        private readonly Dictionary<(AnalysisModuleKey Module, string Name), IndexSpan> _imports;
        private readonly Dictionary<string, IndexSpan> _exports;
        private readonly PythonAst _ast;
        private readonly IOSPlatform _platformService;
        private readonly PathResolverSnapshot _pathResolver;
        private readonly string _filePath;
        private readonly bool _isTypeshed;
        private int _depth;

        public IEnumerable<(AnalysisModuleKey Module, string Name, IndexSpan Location)> Imports 
            => _imports.Select(kvp => (kvp.Key.Module, kvp.Key.Name, kvp.Value));
        public IEnumerable<(string Name, IndexSpan Location)> Exports 
            => _exports.Select(kvp => (kvp.Key, kvp.Value));

        public ImportExportWalker(PythonAst ast, IOSPlatform platformService, PathResolverSnapshot pathResolver, string filePath, bool isTypeshed) {
            _imports = new Dictionary<(AnalysisModuleKey Module, string Name), IndexSpan>();
            _exports = new Dictionary<string, IndexSpan>();
            _ast = ast;
            _platformService = platformService;
            _pathResolver = pathResolver;
            _isTypeshed = isTypeshed;
            _filePath = filePath;
        }

        public void Walk() => _ast.Walk(this);

        public override bool Walk(AssignmentStatement node) {
            if (_depth == 0) {
                HandleAssignment(node);
            }
            return base.Walk(node);
        }

        private void HandleAssignment(AssignmentStatement node) {
            foreach (var expr in node.Left.Select(s => s.RemoveParenthesis()).OfType<NameExpression>()) {
                AddExport(expr.Name, expr.IndexSpan);
            }

            if (node.Right is MemberExpression me) {
                AddImportIfModule(me);
            }
        }

        public override bool Walk(IfStatement node) => node.WalkIfWithSystemConditions(this, _ast.LanguageVersion, _platformService);

        public override bool Walk(ImportStatement expr) {
            if (_depth > 0) {
                return false;
            }

            var len = Math.Min(expr.Names.Count, expr.AsNames.Count);
            var forceAbsolute = expr.ForceAbsolute;
            for (var i = 0; i < len; i++) {
                var moduleImportExpression = expr.Names[i];
                var asNameExpression = expr.AsNames[i];

                if (!string.IsNullOrEmpty(asNameExpression?.Name)) {
                    AddLastModuleImport(moduleImportExpression, asNameExpression, forceAbsolute);
                } else {
                    AddAllImports(moduleImportExpression, forceAbsolute);
                }
            }

            return false;
        }

        private void AddLastModuleImport(ModuleName importExpression, NameExpression importName, bool forceAbsolute) {
            var result = _pathResolver.GetImportsFromAbsoluteName(_filePath, importExpression.Names.Select(n => n.Name), forceAbsolute);
            if (result is ModuleImport mi) {
                AddImport(mi, default, importName.IndexSpan);
            }
        }

        private void AddAllImports(ModuleName moduleImportExpression, bool forceAbsolute) {
            var importNames = ImmutableArray<string>.Empty;

            for (var i = 0; i < moduleImportExpression.Names.Count; i++) {
                var nameExpression = moduleImportExpression.Names[i];
                importNames = importNames.Add(nameExpression.Name);
                var result = _pathResolver.GetImportsFromAbsoluteName(_filePath, importNames, forceAbsolute);
                if (result is ModuleImport mi && !mi.ModulePath.PathEquals(_filePath)) {
                    AddImport(mi, default, nameExpression.IndexSpan);
                    if (i == 0) {
                        AddExport(nameExpression.Name, nameExpression.IndexSpan);
                    }
                }
            }
        }

        public override bool Walk(FromImportStatement expr) {
            if (_depth > 0) {
                return base.Walk(expr);
            }

            var rootNames = expr.Root.Names;
            if (rootNames.Count == 1 && rootNames[0].Name.EqualsOrdinal("__future__")) {
                return base.Walk(expr);
            }

            var imports = _pathResolver.FindImports(_filePath, expr);
            if (!(imports is ModuleImport mi)) {
                return base.Walk(expr);
            }

            var names = expr.Names;
            var asNames = expr.AsNames;
            if (names.Count == 1 && names[0].Name == "*") {
                AddImport(mi, default, names[0].IndexSpan);
                return base.Walk(expr);
            }

            for (var i = 0; i < names.Count; i++) {
                var memberName = names[i].Name;
                if (string.IsNullOrEmpty(memberName)) {
                    continue;
                }

                var nameExpression = asNames[i] ?? names[i];
                if (mi.TryGetChildImport(nameExpression.Name, out var child) && child is ModuleImport childMi) {
                    AddImport(childMi, default, nameExpression.IndexSpan);
                } else {
                    AddImport(mi, names[i].Name, nameExpression.IndexSpan);
                }

                AddExport(nameExpression.Name, nameExpression.IndexSpan);
            }

            return base.Walk(expr);
        }

        public override bool Walk(MemberExpression expr) {
            if (_depth == 0) {
                AddImportIfModule(expr);
            }

            return base.Walk(expr);
        }

        public override bool Walk(ClassDefinition cd) {
            if (_depth == 0 && !string.IsNullOrEmpty(cd.Name)) {
                AddExport(cd.Name, cd.NameExpression.IndexSpan);
            }
            _depth++;
            return base.Walk(cd);
        }

        public override void PostWalk(ClassDefinition cd) {
            _depth--;
            base.PostWalk(cd);
        }

        public override bool Walk(FunctionDefinition fd) {
            if (_depth == 0 && !string.IsNullOrEmpty(fd.Name)) {
                AddExport(fd.Name, fd.NameExpression.IndexSpan);
            }
            _depth++;
            return base.Walk(fd);
        }

        public override void PostWalk(FunctionDefinition fd) {
            _depth--;
            base.PostWalk(fd);
        }

        private void AddExport(in string name, IndexSpan location) {
            if (!_exports.TryGetValue(name, out var current) || current.Start > location.Start) {
                _exports[name] = location;
            }
        }

        private void AddImportIfModule(in MemberExpression expr) {
            var currentExpression = expr;
            var memberExpressions = new Stack<MemberExpression>();
            memberExpressions.Push(currentExpression);

            while (currentExpression.Target is MemberExpression me) {
                memberExpressions.Push(me);
                currentExpression = me;
            }

            if (!(currentExpression.Target is NameExpression ne)) {
                return;
            }

            var import = _pathResolver.GetModuleImportFromModuleName(ne.Name);
            if (import == null) {
                return;
            }

            var moduleKey = new AnalysisModuleKey(import.Name, import.ModulePath, _isTypeshed);
            IImportChildrenSource childrenSource = _pathResolver.GetModuleImportFromModuleName(moduleKey.Name);
            if (childrenSource == default) {
                return;
            }

            while (memberExpressions.Count > 0) {
                var expression = memberExpressions.Pop();

                if (!childrenSource.TryGetChildImport(expression.Name, out var child)) {
                    AddImport(moduleKey, expression.Name, expression.IndexSpan);
                    return;
                }

                if (child is IImportChildrenSource cs) {
                    childrenSource = cs;
                } else {
                    return;
                }
            }
        }

        private void AddImport(in ModuleImport moduleImport, in string name, in IndexSpan location) 
            => AddImport(new AnalysisModuleKey(moduleImport.FullName, moduleImport.ModulePath, _isTypeshed), name, location);

        private void AddImport(in AnalysisModuleKey key, in string name, in IndexSpan location) {
            if (key.FilePath.PathEquals(_filePath)) {
                return;
            }

            if (_imports.TryGetValue((key, name), out var current) && current.Start <= location.Start) {
                return;
            }

            _imports[(key, name)] = location;
        }
    }
}
