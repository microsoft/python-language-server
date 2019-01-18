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

using System.Collections.Generic;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis {
    public interface IModule : IAnalysisValue {
        IPythonProjectEntry ProjectEntry { get; }
        IScope Scope { get; }
        IModule GetChildPackage(IModuleContext context, string name);
        IEnumerable<KeyValuePair<string, AnalysisValue>> GetChildrenPackages(IModuleContext context);
        IEnumerable<string> GetModuleMemberNames(IModuleContext context);
        void Imported(AnalysisUnit unit);
        void SpecializeFunction(string name, CallDelegate callable, bool mergeOriginalAnalysis);
        IAnalysisSet GetModuleMember(Node node, AnalysisUnit unit, string name, bool addRef, IScope linkedScope, string linkedName);
    }
}
