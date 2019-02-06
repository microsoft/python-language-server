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

using Microsoft.PythonTools.Analysis.Values;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Analyzer {
    sealed class ClassScope : InterpreterScope, IClassScope {
        public ClassScope(ClassInfo classInfo, ClassDefinition ast, InterpreterScope outerScope)
            : base(classInfo, ast, outerScope) {
            classInfo.Scope = this;
        }

        public ClassInfo Class => (ClassInfo)AnalysisValue;
        IClassInfo IClassScope.Class => Class;

        public override int GetBodyStart(PythonAst ast) => ((ClassDefinition)Node).HeaderIndex;

        public override string Name => Class.ClassDefinition.Name;

        public override bool VisibleToChildren => false;

        public override bool AssignVariable(string name, Node location, AnalysisUnit unit, IAnalysisSet values) {
            var res = base.AssignVariable(name, location, unit, values);

            if (name == "__metaclass__") {
                // assignment to __metaclass__, save it in our metaclass variable
                Class.GetOrCreateMetaclassVariable().AddTypes(unit, values);
            }

            return res;
        }
    }
}
