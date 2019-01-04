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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Analyzer.Evaluation;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    internal sealed class ClassEvalWalker : MemberEvalWalker {
        private readonly ClassDefinition _target;
        private IDisposable _classScope;
        private PythonClassType _class;

        public ClassEvalWalker(ExpressionEval eval, ClassDefinition target) : base(eval, target) {
            _target = target;
        }

        public override async Task WalkAsync(CancellationToken cancellationToken = default)
            => await _target.WalkAsync(this, cancellationToken);

        public override async Task<bool> WalkAsync(ClassDefinition node, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();

            // Open proper scope chain
            using (Eval.OpenScope(Target, out var outerScope)) {
                var instance = Eval.GetInScope(node.Name, outerScope);
                if (!(instance?.GetPythonType() is PythonClassType classInfo)) {
                    if (instance != null) {
                        // TODO: warning that variable is already declared of a different type.
                    }
                    // May be odd case like class inside a class.
                    return false;
                }

                _class = classInfo;
                // Set bases to the class.
                var bases = node.Bases.Where(a => string.IsNullOrEmpty(a.Name))
                    // We cheat slightly and treat base classes as annotations.
                    .Select(a => Eval.GetTypeFromAnnotation(a.Expression))
                    .ToArray();
                _class.SetBases(Interpreter, bases);

                // Open new scope for the class off the parent scope that
                // was recorded when walker for this class was created.
                _classScope = Eval.OpenScope(node, out _);
                // Declare __class__ variable in the scope.
                Eval.DeclareVariable("__class__", _class, node);

                await ProcessClassStatements(node, cancellationToken);
            }
            // We are done.
            return false;
        }

        public override Task PostWalkAsync(ClassDefinition node, CancellationToken cancellationToken = default) {
            if (_class != null) {
                // Add members from this file
                _class.AddMembers(Eval.CurrentScope.Variables, false);

                // Add members from stub
                var stubClass = Eval.Module.Stub?.GetMember<IPythonClassType>(_class.Name);
                _class.AddMembers(stubClass, false);
                _classScope?.Dispose();
            }

            return base.PostWalkAsync(node, cancellationToken);
        }

        private async Task ProcessClassStatements(ClassDefinition node, CancellationToken cancellationToken = default) {
            // Class is handled in a specific order rather than in the order of
            // the statement appearance. This is because we need all members
            // properly declared and added to the class type so when we process
            // methods, the class variables are all declared and constructors
            // are evaluated.

            // Process imports
            foreach (var s in GetStatements<FromImportStatement>(node)) {
                await FromImportHandler.HandleFromImportAsync(s, cancellationToken);
            }
            foreach (var s in GetStatements<ImportStatement>(node)) {
                await ImportHandler.HandleImportAsync(s, cancellationToken);
            }

            // Process assignments so we get annotated class members declared.
            foreach (var s in GetStatements<AssignmentStatement>(node)) {
                await AssignmentHandler.HandleAssignmentAsync(s, cancellationToken);
            }
            foreach (var s in GetStatements<ExpressionStatement>(node)) {
                await AssignmentHandler.HandleAnnotatedExpressionAsync(s.Expression as ExpressionWithAnnotation, null, cancellationToken);
            }

            // Ensure constructors are processed so class members are initialized.
            await SymbolTable.ProcessConstructorsAsync(node, cancellationToken);
            // Process bases.
            foreach (var b in _class.Bases.Select(b => b.GetPythonType<IPythonClassType>()).ExcludeDefault()) {
                await SymbolTable.ProcessMemberAsync(b.ClassDefinition, cancellationToken);
            }
            await SymbolTable.ProcessConstructorsAsync(node, cancellationToken);
            // Process remaining methods.
            await SymbolTable.ProcessScopeMembersAsync(node, cancellationToken);
        }
    }
}
