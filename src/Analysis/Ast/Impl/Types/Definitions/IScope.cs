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
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Types {
    /// <summary>
    /// Represents scope where variables can be declared.
    /// </summary>
    public interface IScope {
        string Name { get; }
        Node Node { get; }
        IScope OuterScope { get; }
        IGlobalScope GlobalScope { get; }
        bool VisibleToChildren { get; }
        IReadOnlyList<IScope> Children { get; }
        IEnumerable<IScope> EnumerateTowardsGlobal { get; }
        IEnumerable<IScope> EnumerateFromGlobal { get; }
        IVariableCollection Variables { get; }
    }
}
