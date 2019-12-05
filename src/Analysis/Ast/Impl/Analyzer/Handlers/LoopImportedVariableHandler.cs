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
using Microsoft.Python.Analysis.Analyzer.Evaluation;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Handlers {
    internal sealed class LoopImportedVariableHandler : IImportedVariableHandler {
        private readonly Dictionary<AnalysisModuleKey, ModuleWalker> _walkers = new Dictionary<AnalysisModuleKey, ModuleWalker>();
        private readonly IReadOnlyDictionary<AnalysisModuleKey, PythonAst> _asts;
        private readonly IReadOnlyDictionary<AnalysisModuleKey, IVariableCollection> _cachedVariables;
        private readonly IServiceContainer _services;
        private readonly Func<bool> _isCanceled;

        public IReadOnlyCollection<ModuleWalker> Walkers => _walkers.Values;

        public LoopImportedVariableHandler(in IServiceContainer services,
            in IReadOnlyDictionary<AnalysisModuleKey, PythonAst> asts,
            in IReadOnlyDictionary<AnalysisModuleKey, IVariableCollection> cachedVariables,
            in Func<bool> isCanceled) {

            _services = services;
            _isCanceled = isCanceled;
            _asts = asts;
            _cachedVariables = cachedVariables;
        }

        public IEnumerable<string> GetMemberNames(PythonVariableModule variableModule) {
            var module = variableModule.Module;
            if (module == null || _isCanceled()) {
                return Enumerable.Empty<string>();
            }

            var key = new AnalysisModuleKey(module);
            if (_walkers.TryGetValue(key, out var walker)) {
                return GetMemberNames(walker, variableModule);
            }

            if (!_asts.TryGetValue(key, out var ast)) {
                return _cachedVariables.TryGetValue(key, out var variables)
                    ? variables.Names
                    : variableModule.GetMemberNames().Where(s => !s.StartsWithOrdinal("_"));
            }

            walker = WalkModule(module, ast);
            return walker != null ? GetMemberNames(walker, variableModule) : module.GetMemberNames();
        }

        public IVariable GetVariable(in PythonVariableModule variableModule, in string name) {
            var module = variableModule.Module;
            if (module == null || _isCanceled()) {
                return default;
            }

            var key = new AnalysisModuleKey(module);
            if (_walkers.TryGetValue(key, out var walker)) {
                return walker.Eval.GlobalScope?.Variables[name];
            }

            if (!_asts.TryGetValue(key, out var ast)) {
                return _cachedVariables.TryGetValue(key, out var variables) ? variables[name] : default;
            }

            _walkers[key] = walker = WalkModule(module, ast);
            var gs = walker != null ? walker.Eval.GlobalScope : module.GlobalScope;
            return gs?.Variables[name];
        }

        public void EnsureModule(in PythonVariableModule variableModule) {
            var module = variableModule.Module;
            if (module == null || _isCanceled()) {
                return;
            }
            EnsureModule(module);
        }

        public void EnsureModule(in IPythonModule module) {
            if (module == null || _isCanceled()) {
                return;
            }
            var key = new AnalysisModuleKey(module);
            if (!_walkers.ContainsKey(key) && _asts.TryGetValue(key, out var ast)) {
                WalkModule(module, ast);
            }
        }

        public ModuleWalker WalkModule(IPythonModule module, PythonAst ast) {
            // If module has stub, make sure it is processed too.
            if (module.Stub?.Analysis is EmptyAnalysis) {
                WalkModule(module.Stub, module.Stub.GetAst());
            }

            var eval = new ExpressionEval(_services, module, ast);
            var walker = new ModuleWalker(eval, this);

            _walkers[new AnalysisModuleKey(module)] = walker;
            ast.Walk(walker);
            walker.Complete();
            return walker;
        }

        private static IEnumerable<string> GetMemberNames(ModuleWalker walker, PythonVariableModule variableModule)
            => walker.StarImportMemberNames ?? walker.GlobalScope.GetExportableVariableNames().Concat(variableModule.ChildrenNames).Distinct().Where(s => !s.StartsWithOrdinal("_"));
    }
}
