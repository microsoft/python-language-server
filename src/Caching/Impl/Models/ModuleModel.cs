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
using System.Linq;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Caching.Models {
    internal sealed class ModuleModel : MemberModel {
        public FunctionModel[] Functions { get; set; }
        public VariableModel[] Variables { get; set; }
        public ClassModel[] Classes { get; set; }
        // TODO: TypeVars, ...

        public static ModuleModel FromAnalysis(IDocumentAnalysis analysis) {
            var variables = new List<VariableModel>();
            var functions = new List<FunctionModel>();
            var classes = new List<ClassModel>();

            foreach (var v in analysis.GlobalScope.Variables.Where(v => v.Source == VariableSource.Declaration)) {
                var t = v.Value.GetPythonType();
                // If variable is declaration and has location, then it is a user-defined variable.
                if (v.Location.IsValid) {
                    variables.Add(VariableModel.FromVariable(v));
                    continue;
                }
                switch (t) {
                    case IPythonFunctionType ft when ft.DeclaringModule.Equals(analysis.Document):
                        functions.Add(FunctionModel.FromType(ft));
                        break;
                    case IPythonClassType cls when cls.DeclaringModule.Equals(analysis.Document):
                        classes.Add(ClassModel.FromType(cls));
                        break;
                }
            }

            return new ModuleModel {
                Name = analysis.Document.GetQualifiedName(),
                Documentation = analysis.Document.Documentation,
                Functions = functions.ToArray(),
                Variables = variables.ToArray(),
                Classes = classes.ToArray()
            };
        }
    }
}
