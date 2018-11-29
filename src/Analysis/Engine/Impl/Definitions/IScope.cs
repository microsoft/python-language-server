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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Collections.Generic;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis {
    public interface IScope {
        string Name { get; }
        Node Node { get; }
        IScope OuterScope { get; }
        IReadOnlyList<IScope> Children { get; }
        bool ContainsImportStar { get; }
        IScope GlobalScope { get; }
        AnalysisValue AnalysisValue { get; }

        IEnumerable<IScope> EnumerateTowardsGlobal { get; }
        IEnumerable<IScope> EnumerateFromGlobal { get; }
        int GetBodyStart(PythonAst ast);
        int GetStart(PythonAst ast);
        int GetStop(PythonAst ast);
        IEnumerable<KeyValuePair<string, IVariableDefinition>> AllVariables { get; }
        bool ContainsVariable(string name);
        IVariableDefinition GetVariable(string name);
        bool TryGetVariable(string name, out IVariableDefinition value);
        int VariableCount { get; }
        bool VisibleToChildren { get; }
        IEnumerable<IVariableDefinition> GetMergedVariables(string name);
        IEnumerable<IVariableDefinition> GetLinkedVariables(string name);
        IAnalysisSet GetMergedVariableTypes(string name);
    }
}
