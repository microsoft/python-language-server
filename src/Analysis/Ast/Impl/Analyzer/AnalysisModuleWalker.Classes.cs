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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    internal sealed partial class AnalysisModuleWalker {
        public override Task<bool> WalkAsync(ClassDefinition node, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();

            var instance = Lookup.GetInScope(node.Name);
            if (instance != null && !(instance.GetPythonType() is PythonClassType)) {
                // TODO: warning that variable is already declared.
                return Task.FromResult(false);
            }

            if (!(instance.GetPythonType() is PythonClassType classInfo)) {
                classInfo = CreateClass(node);
                Lookup.DeclareVariable(node.Name, classInfo, node);
            }

            var bases = node.Bases.Where(a => string.IsNullOrEmpty(a.Name))
                // We cheat slightly and treat base classes as annotations.
                .Select(a => Lookup.GetTypeFromAnnotation(a.Expression))
                .ToArray();

            classInfo.SetBases(Interpreter, bases);
            _classScope = Lookup.CreateScope(node, Lookup.CurrentScope);
            Lookup.DeclareVariable("__class__", classInfo, node);

            return Task.FromResult(true);
        }

        public override Task PostWalkAsync(ClassDefinition node, CancellationToken cancellationToken = default) {
            var cls = Lookup.GetInScope("__class__")?.GetPythonType() as PythonClassType;
            Debug.Assert(cls != null || Lookup.GetInScope("__class__") == null, "__class__ variable is not a PythonClass.");
            if (cls != null) {
                // Add members from this file
                cls.AddMembers(Lookup.CurrentScope.Variables, true);

                // Add members from stub
                var stubClass = Lookup.Module.Stub?.GetMember<IPythonClassType>(cls.Name);
                cls.AddMembers(stubClass, false);
                _classScope?.Dispose();
            }

            return base.PostWalkAsync(node, cancellationToken);
        }

        private PythonClassType CreateClass(ClassDefinition node) {
            node = node ?? throw new ArgumentNullException(nameof(node));
            return new PythonClassType(
                node,
                Module,
                GetDoc(node.Body as SuiteStatement),
                GetLoc(node),
                Interpreter,
                Lookup.SuppressBuiltinLookup ? BuiltinTypeId.Unknown : BuiltinTypeId.Type); // built-ins set type later
        }
    }
}
