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
using System.Linq;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing.Ast;
using Microsoft.Python.Parsing.Definition;

namespace Microsoft.Python.Analysis.Values {
    internal class EmptyGlobalScope : IGlobalScope {
        public EmptyGlobalScope(IPythonModule module) {
            GlobalScope = this;
            Module = module;
        }

        public IPythonModule Module { get; }
        public string Name => string.Empty;
        public IScopeNode Node => Module.Analysis.Ast;
        public IScope OuterScope => null;
        public IGlobalScope GlobalScope { get; }
        public IReadOnlyList<IScope> Children => Array.Empty<IScope>();
        public IEnumerable<IScope> EnumerateTowardsGlobal => Enumerable.Repeat(this, 1);
        public IEnumerable<IScope> EnumerateFromGlobal => Enumerable.Repeat(this, 1);
        public IVariableCollection Variables => VariableCollection.Empty;
        public IVariableCollection NonLocals => VariableCollection.Empty;
        public IVariableCollection Globals => VariableCollection.Empty;
        public IVariableCollection Imported => VariableCollection.Empty;

        public void DeclareVariable(string name, IMember value, VariableSource source, Location location) { }
        public void LinkVariable(string name, IVariable v, Location location) { }
    }
}
