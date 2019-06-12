﻿// Copyright(c) Microsoft Corporation
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

            // Go directly through variables rather than GetMemberNames/GetMember since
            // module may have non-exported variables and types that it may be returning
            // from functions and methods or otherwise using in declarations.
            foreach (var v in analysis.GlobalScope.Variables.Where(v => v.Source == VariableSource.Declaration)) {
                var t = v.Value.GetPythonType();
                // Create type model before variable since variable needs it.
                string typeName = null;
                switch (v.Value) {
                    case IPythonFunctionType ft when ft.DeclaringModule.Equals(analysis.Document):
                        if (!functions.ContainsKey(ft.Name)) {
                            typeName = ft.Name;
                            functions[ft.Name] = FunctionModel.FromType(ft);
                        }

                        break;
                    case IPythonClassType cls when cls.DeclaringModule.Equals(analysis.Document):
                        if (!classes.ContainsKey(cls.Name)) {
                            typeName = cls.Name;
                            classes[cls.Name] = ClassModel.FromType(cls);
                        }
                        break;
                }

                // Do not re-declare classes and functions as variables in the model.
                if (typeName == null && !variables.ContainsKey(v.Name)) {
                    variables[v.Name] = VariableModel.FromVariable(v);
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
