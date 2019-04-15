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

using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Values {
    /// <summary>
    /// Provides ability to lookup scope by the scope statement.
    /// Implemented by expression evaluator attached to the analysis.
    /// </summary>
    internal interface IScopeLookup {
        void AddScope(Scope scope, ScopeStatement statement);
        Scope GetScope(ScopeStatement statement);
    }
}
