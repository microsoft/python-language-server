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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    public interface IExpressionEvaluator {
        /// <summary>
        /// Opens existing scope for a node. The scope is pushed
        /// on the stack and will be removed when the returned
        /// disposable is disposed.
        /// </summary>
        IDisposable OpenScope(IScope scope);

        /// <summary>
        /// Opens existing scope for a node. The scope is pushed
        /// on the stack and will be removed when the returned
        /// disposable is disposed.
        /// </summary>
        IDisposable OpenScope(ScopeStatement scope);

        /// <summary>
        /// Currently opened (deep-most) scope.
        /// </summary>
        IScope CurrentScope { get; }

        /// <summary>
        /// Module global scope.
        /// </summary>
        IGlobalScope GlobalScope { get; }

        /// <summary>
        /// Determines node location in the module source code.
        /// </summary>
        LocationInfo GetLocation(Node node);

        /// <summary>
        /// Evaluates expression in the currently open scope.
        /// </summary>
        Task<IMember> GetValueFromExpressionAsync(Expression expr, CancellationToken cancellationToken = default);

        IMember LookupNameInScopes(string name, out IScope scope);

        IPythonType TryGetTypeFromPepHint(Node node);

        PythonAst Ast { get; }
        IPythonModule Module { get; }
        IPythonInterpreter Interpreter { get; }
        IServiceContainer Services { get; }
    }
}
