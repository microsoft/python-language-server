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
using Microsoft.Python.Analysis.Analyzer.Evaluation;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Symbols {
    internal sealed class ClassEvaluator : MemberEvaluator {
        private readonly ClassDefinition _classDef;
        private PythonClassType _class;

        public ClassEvaluator(ExpressionEval eval, ClassDefinition classDef) : base(eval, classDef) {
            _classDef = classDef;
        }

        public override Task EvaluateAsync(CancellationToken cancellationToken = default)
            => EvaluateClassAsync(cancellationToken);

        public async Task EvaluateClassAsync(CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();

            // Open class scope chain
            using (Eval.OpenScope(Module, _classDef, out var outerScope)) {
                var instance = Eval.GetInScope(_classDef.Name, outerScope);
                if (!(instance?.GetPythonType() is PythonClassType classInfo)) {
                    if (instance != null) {
                        // TODO: warning that variable is already declared of a different type.
                    }
                    return;
                }

                // Evaluate inner classes, if any
                await EvaluateInnerClassesAsync(_classDef, cancellationToken);

                _class = classInfo;
                // Set bases to the class.
                var bases = new List<IPythonType>();
                foreach (var a in _classDef.Bases.Where(a => string.IsNullOrEmpty(a.Name))) {
                    // We cheat slightly and treat base classes as annotations.
                    var b = await Eval.GetTypeFromAnnotationAsync(a.Expression, cancellationToken);
                    if (b != null) {
                        bases.Add(b.GetPythonType());
                    }
                }
                _class.SetBases(bases);

                // Declare __class__ variable in the scope.
                Eval.DeclareVariable("__class__", _class, VariableSource.Declaration, _classDef);

                await ProcessClassBody(cancellationToken);
            }
        }

        private async Task ProcessClassBody(CancellationToken cancellationToken = default) {
            // Class is handled in a specific order rather than in the order of
            // the statement appearance. This is because we need all members
            // properly declared and added to the class type so when we process
            // methods, the class variables are all declared and constructors
            // are evaluated.

            // Process bases.
            foreach (var b in _class.Bases.Select(b => b.GetPythonType<IPythonClassType>()).ExcludeDefault()) {
                await SymbolTable.EvaluateAsync(b.ClassDefinition, cancellationToken);
            }

            // Process imports
            foreach (var s in GetStatements<FromImportStatement>(_classDef)) {
                ImportHandler.HandleFromImportAsync(s, cancellationToken);
            }

            foreach (var s in GetStatements<ImportStatement>(_classDef)) {
                ImportHandler.HandleImportAsync(s, cancellationToken);
            }

            UpdateClassMembers();

            // Process assignments so we get class variables declared.
            foreach (var s in GetStatements<AssignmentStatement>(_classDef)) {
                await AssignmentHandler.HandleAssignmentAsync(s, cancellationToken);
            }
            foreach (var s in GetStatements<ExpressionStatement>(_classDef)) {
                await AssignmentHandler.HandleAnnotatedExpressionAsync(s.Expression as ExpressionWithAnnotation, null, cancellationToken);
            }
            UpdateClassMembers();

            // Ensure constructors are processed so class members are initialized.
            await EvaluateConstructorsAsync(_classDef, cancellationToken);
            UpdateClassMembers();

            // Process remaining methods.
            await SymbolTable.EvaluateScopeAsync(_classDef, cancellationToken);
            UpdateClassMembers();
        }

        private async Task EvaluateConstructorsAsync(ClassDefinition cd, CancellationToken cancellationToken = default) {
            // Do not use foreach since walker list is dynamically modified and walkers are removed
            // after processing. Handle __init__ and __new__ first so class variables are initialized.
            var constructors = SymbolTable.Evaluators
                .Where(kvp => kvp.Key.Parent == cd && (kvp.Key.Name == "__init__" || kvp.Key.Name == "__new__"))
                .Select(c => c.Value)
                .ExcludeDefault()
                .ToArray();

            foreach (var ctor in constructors) {
                await SymbolTable.EvaluateAsync(ctor, cancellationToken);
            }
        }

        private async Task EvaluateInnerClassesAsync(ClassDefinition cd, CancellationToken cancellationToken = default) {
            // Do not use foreach since walker list is dynamically modified and walkers are removed
            // after processing. Handle __init__ and __new__ first so class variables are initialized.
            var innerClasses = SymbolTable.Evaluators
                .Where(kvp => kvp.Key.Parent == cd && (kvp.Key is ClassDefinition))
                .Select(c => c.Value)
                .ExcludeDefault()
                .ToArray();

            foreach (var c in innerClasses) {
                await SymbolTable.EvaluateAsync(c, cancellationToken);
            }
        }

        private void UpdateClassMembers() {
            // Add members from this file
            var members = Eval.CurrentScope.Variables.Where(v => v.Source == VariableSource.Declaration || v.Source == VariableSource.Import);
            _class.AddMembers(members, false);
            // Add members from stub
            var stubClass = Eval.Module.Stub?.GetMember<IPythonClassType>(_class.Name);
            _class.AddMembers(stubClass, false);
        }

        // Classes and functions are walked by their respective evaluators
        public override Task<bool> WalkAsync(ClassDefinition node, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public override Task<bool> WalkAsync(FunctionDefinition node, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
