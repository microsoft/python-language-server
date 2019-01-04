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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Analyzer.Evaluation;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    internal sealed class ClassWalker: MemberWalker {
        private readonly ClassDefinition _target;
        private IDisposable _classScope;
        private PythonClassType _class;

        public ClassWalker(ExpressionEval eval, ClassDefinition target) : base(eval, target) {
            _target = target;
        }

        public override async Task WalkAsync(CancellationToken cancellationToken = default) 
            => await _target.WalkAsync(this, cancellationToken);

        public override async Task<bool> WalkAsync(ClassDefinition node, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();

            // Open proper scope chain
            using (Eval.OpenScope(Target, out var outerScope)) {
                // Class is handled as follows:
                //  - Collect member functions definitions for forward reference resolution.
                //  - Create class type, declare variable representing class type info in the current scope.
                //  - Set bases to the class
                //  - Open new scope for the class.
                //  - Declare __class__ variable in the scope.
                //  - Declare 'self' for the class
                //  - Process assignments so we get annotated class members declared.
                //  - Process constructors which may declare more members for the class.
                //  - Process class methods.

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

                // Collect member functions definitions for forward reference resolution.
                CollectMemberDefinitions(Target);

                // Process assignments so we get annotated class members declared.
                foreach (var s in GetStatements<AssignmentStatement>(node)) {
                    await HandleAssignmentAsync(s, cancellationToken);
                }

                // Ensure constructors are processed so class members are initialized.
                await MemberWalkers.ProcessConstructorsAsync(node, cancellationToken);
                // Process remaining methods.
                await MemberWalkers.ProcessMembersAsync(node, cancellationToken);
            }
            // We are done.
            return false;
        }

        public override Task PostWalkAsync(ClassDefinition node, CancellationToken cancellationToken = default) {
            if (_class != null) {
                // Add members from this file
                _class.AddMembers(Eval.CurrentScope.Variables, true);

                // Add members from stub
                var stubClass = Eval.Module.Stub?.GetMember<IPythonClassType>(_class.Name);
                _class.AddMembers(stubClass, false);
                _classScope?.Dispose();
            }

            return base.PostWalkAsync(node, cancellationToken);
        }
    }
}
