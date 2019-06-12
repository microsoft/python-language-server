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
using System.Diagnostics;
using System.Linq;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Caching.Models {
    internal sealed class ModuleModel : MemberModel {
        public string Documentation { get; set; }
        public FunctionModel[] Functions { get; set; }
        public VariableModel[] Variables { get; set; }
        public ClassModel[] Classes { get; set; }
        // TODO: TypeVars, ...

        public static ModuleModel FromAnalysis(IDocumentAnalysis analysis) {
            var variables = new Dictionary<string, VariableModel>();
            var functions = new Dictionary<string, FunctionModel>();
            var classes = new Dictionary<string, ClassModel>();

            foreach (var v in analysis.GlobalScope.Variables.Where(v => v.Source == VariableSource.Declaration)) {
                var t = v.Value.GetPythonType();
                switch (v.Value) {
                    case IPythonFunctionType ft when ft.DeclaringModule.Equals(analysis.Document):
                        Debug.Assert(!functions.ContainsKey(ft.Name));
                        functions[ft.Name] = FunctionModel.FromType(ft);
                        break;
                    case IPythonClassType cls when cls.DeclaringModule.Equals(analysis.Document):
                        Debug.Assert(!classes.ContainsKey(cls.Name));
                        classes[cls.Name] = ClassModel.FromType(cls);
                        break;
                    default:
                        Debug.Assert(!variables.ContainsKey(v.Name));
                        variables[v.Name] = VariableModel.FromVariable(v);
                        break;
                }
            }

            return new ModuleModel {
                Name = analysis.Document.GetQualifiedName(),
                Documentation = analysis.Document.Documentation,
                Functions = functions.Values.ToArray(),
                Variables = variables.Values.ToArray(),
                Classes = classes.Values.ToArray()
            };
        }
    }
}
