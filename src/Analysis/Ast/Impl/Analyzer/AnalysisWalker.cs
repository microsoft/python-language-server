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
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Core.Logging;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    [DebuggerDisplay("{_module.Name} : {_module.ModuleType}")]
    internal sealed partial class AnalysisWalker : PythonWalkerAsync {
        private readonly IServiceContainer _services;
        private readonly IPythonInterpreter _interpreter;
        private readonly ILogger _log;
        private readonly IPythonModule _module;
        private readonly PythonAst _ast;
        private readonly ExpressionLookup _lookup;
        private readonly GlobalScope _globalScope;
        private readonly AnalysisFunctionWalkerSet _functionWalkers = new AnalysisFunctionWalkerSet();
        private readonly HashSet<FunctionDefinition> _replacedByStubs = new HashSet<FunctionDefinition>();
        private readonly bool _suppressBuiltinLookup;
        private IDisposable _classScope;

        public AnalysisWalker(IServiceContainer services, IPythonModule module, PythonAst ast, bool suppressBuiltinLookup) {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _module = module ?? throw new ArgumentNullException(nameof(module));
            _ast = ast ?? throw new ArgumentNullException(nameof(ast));

            _interpreter = services.GetService<IPythonInterpreter>();
            _log = services.GetService<ILogger>();
            _globalScope = new GlobalScope(module);
            _lookup = new ExpressionLookup(services, module, ast, _globalScope, _functionWalkers, suppressBuiltinLookup);
            _suppressBuiltinLookup = suppressBuiltinLookup;
            // TODO: handle typing module
        }

        public IGlobalScope GlobalScope => _globalScope;

        public override async Task<bool> WalkAsync(PythonAst node, CancellationToken cancellationToken = default) {
            Check.InvalidOperation(() => _ast == node, "walking wrong AST");

            CollectTopLevelDefinitions();
            cancellationToken.ThrowIfCancellationRequested();

            return await base.WalkAsync(node, cancellationToken);
        }

        public async Task<IGlobalScope> CompleteAsync(CancellationToken cancellationToken = default) {
            await _functionWalkers.ProcessSetAsync(cancellationToken);
            foreach (var childModuleName in _module.GetChildrenModuleNames()) {
                var name = $"{_module.Name}.{childModuleName}";
                _globalScope.DeclareVariable(name, _module, LocationInfo.Empty);
            }
            return GlobalScope;
        }

        internal LocationInfo GetLoc(ClassDefinition node) {
            if (node == null || node.StartIndex >= node.EndIndex) {
                return null;
            }

            var start = node.NameExpression?.GetStart(_ast) ?? node.GetStart(_ast);
            var end = node.GetEnd(_ast);
            return new LocationInfo(_module.FilePath, _module.Uri, start.Line, start.Column, end.Line, end.Column);
        }

        private LocationInfo GetLoc(Node node) => _lookup.GetLoc(node);

        private void CollectTopLevelDefinitions() {
            var statement = (_ast.Body as SuiteStatement)?.Statements.ToArray() ?? Array.Empty<Statement>();

            foreach (var node in statement.OfType<FunctionDefinition>()) {
                ProcessFunctionDefinition(node);
            }

            foreach (var node in statement.OfType<ClassDefinition>()) {
                var classInfo = CreateClass(node);
                _lookup.DeclareVariable(node.Name, classInfo, GetLoc(node));
            }
        }
    }
}
