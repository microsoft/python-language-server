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
using LiteDB;
using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Caching.Writers {
    internal sealed class ModuleWriter {
        public void WriteModule(LiteDatabase db, string moduleId, IDocumentAnalysis analysis) {
            var variables = new List<VariableModel>();
            var functions = new List<FunctionModel>();
            var classes = new List<ClassModel>();

            foreach (var v in analysis.GlobalScope.Variables) {
                var t = v.Value.GetPythonType();
                // If variable is declaration and has location, then it is a user-defined variable.
                if (v.Source == VariableSource.Declaration && v.Location.IsValid) {
                    variables.Add(VariableModel.FromVariable(v));
                }

                if (v.Source == VariableSource.Declaration && !v.Location.IsValid) {
                    switch (t) {
                        // Typically class or a function
                        case IPythonFunctionType ft: {
                            functions.Add(FunctionModel.FromType(ft));
                            break;
                        }

                        case IPythonClassType cls: {
                            classes.Add(ClassModel.FromType(cls));
                            break;
                        }
                    }
                }
            }

            var variableCollection = db.GetCollection<VariableModel>($"{moduleId}.variables");
            variableCollection.Update(variables);

            var functionCollection = db.GetCollection<FunctionModel>($"{moduleId}.functions");
            functionCollection.Update(functions);

            var classesCollection = db.GetCollection<ClassModel>($"{moduleId}.classes");
            classesCollection.Update(classes);
        }

        // TODO: fix per https://github.com/microsoft/python-language-server/issues/1177
        private string GetModuleUniqueId(IPythonModule module) => module.Name;

    }
}
