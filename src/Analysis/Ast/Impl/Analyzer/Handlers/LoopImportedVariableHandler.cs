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
using Microsoft.Python.Analysis.Analyzer.Evaluation;
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Handlers {
    internal sealed class LoopImportedVariableHandler : IImportedVariableHandler {
        private readonly Dictionary<AnalysisModuleKey, ModuleWalker> _walkers = new Dictionary<AnalysisModuleKey, ModuleWalker>();
        private readonly Dictionary<AnalysisModuleKey, PythonAst> _asts;
        private readonly Dictionary<AnalysisModuleKey, IVariableCollection> _cachedVariables;
        private readonly IServiceContainer _services;

        public IEnumerable<ModuleWalker> Walkers => _walkers.Values;
        
        public LoopImportedVariableHandler(in IServiceContainer services,
            in IEnumerable<(IPythonModule Module, PythonAst Ast)> asts,
            in IEnumerable<(IPythonModule Module, IVariableCollection Variables)> cachedVariables) {
            
            _services = services;
            _asts = asts.ToDictionary(kvp => new AnalysisModuleKey(kvp.Module), kvp => kvp.Ast);
            _cachedVariables = cachedVariables.ToDictionary(kvp => new AnalysisModuleKey(kvp.Module), kvp => kvp.Variables);
        }
        
        public IVariable GetVariable(in PythonVariableModule variableModule, in string name) {
            var module = variableModule.Module;
            if (module == null) {
                return default;
            }

            var key = new AnalysisModuleKey(module);
            if (_walkers.TryGetValue(key, out var walker)) {
                return walker.Eval.GlobalScope?.Variables[name];
            }
            
            if (!_asts.TryGetValue(key, out var ast)) {
                return _cachedVariables.TryGetValue(key, out var variables) ? variables[name] : default;
            }
            
            walker = CreateWalker(module, ast);
            ast.Walk(walker);
            walker.Complete();

            return walker.Eval.GlobalScope?.Variables[name];
        }

        public ModuleWalker CreateWalker(IPythonModule module, PythonAst ast) {
            var eval = new ExpressionEval(_services, module, ast);
            var walker = new ModuleWalker(eval, this);
            _walkers[new AnalysisModuleKey(module)] = walker;
            return walker;
        }
    }
}
