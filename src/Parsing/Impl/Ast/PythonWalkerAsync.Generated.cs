// Python Tools for Visual Studio
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

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Python.Parsing.Ast {
    /// <summary>
    /// PythonWalker class - The Python AST Walker (default result is true)
    /// </summary>
    public class PythonWalkerAsync: PythonWalker {
        public static bool IsNodeWalkAsync(Node node) {
            switch (node) {
                case SuiteStatement s:
                case AssignmentStatement a:
                case FunctionDefinition f:
                case ReturnStatement r:
                    return true;
            }
            return false;
        }

        public virtual Task<bool> WalkAsync(PythonAst node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(PythonAst node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // SuiteStatement
        public virtual Task<bool> WalkAsync(SuiteStatement node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(SuiteStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // AssignmentStatement
        public virtual Task<bool> WalkAsync(AssignmentStatement node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(AssignmentStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;

        // FunctionDefinition
        public virtual Task<bool> WalkAsync(FunctionDefinition node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(FunctionDefinition node, CancellationToken cancellationToken = default) => Task.CompletedTask;
        
        // ReturnStatement
        public virtual Task<bool> WalkAsync(ReturnStatement node, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public virtual Task PostWalkAsync(ReturnStatement node, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
