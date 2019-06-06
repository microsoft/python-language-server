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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using LiteDB;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Caching {
    internal sealed class AnalysisWriter {
        private readonly IServiceContainer _services;

        public AnalysisWriter(IServiceContainer services) {
            _services = services;
        }

        public void StoreModuleAnalysis(string moduleId, IDocumentAnalysis analysis) {
            if(!(analysis is DocumentAnalysis)) {
                return;
            }

            var cfs = _services.GetService<ICacheFolderService>();
            using (var db = new LiteDatabase(Path.Combine(cfs.CacheFolder, "Analysis.db"))) {
                var modules = db.GetCollection<ModuleModel>("modules");
                var md = 
            }
        }

        private void WriteModule(LiteDatabase db, string moduleId, IDocumentAnalysis analysis) {
            var variables = new List<VariableModel>();
            var functions = new List<FunctionModel>();
            var classes = new List<ClassModel>();

            foreach (var v in analysis.GlobalScope.Variables) {
                var t = v.Value.GetPythonType();
                // If variable is declaration and has location, then it is a user-defined variable.
                if(v.Source == VariableSource.Declaration && v.Location.IsValid) {
                    variables.Add(new VariableModel {
                        Name = v.Name,
                        Type = !t.IsUnknown() ? GetTypeQualifiedName(t) : string.Empty
                    });
                }

                if(v.Source == VariableSource.Declaration && !v.Location.IsValid) {
                    switch (t) {
                        // Typically class or a function
                        case IPythonFunctionType ft: {
                            functions.Add(new FunctionModel {
                                Name = ft.Name,
                            });
                            break;
                        }

                        case IPythonClassType cls: {
                            classes.Add(new ClassModel {
                                Name = cls.Name,
                            });
                            break;
                        }
                    }
                }
            }

            var variableCollection = db.GetCollection<VariableModel>(GetVariableCollectionName(moduleId));
            variableCollection.Update(variables);

            var functionCollection = db.GetCollection<FunctionModel>(GetFunctionsCollectionName(moduleId));
            functionCollection.Update(functions);

            var classesCollection = db.GetCollection<ClassModel>(GetClassesCollectionName(moduleId));
            classesCollection.Update(classes);
        }

        private FunctionModel GetFunctionModel(IPythonFunctionType ft) {
            return new FunctionModel {
                Name = ft.Name,
                Overloads = ft.Overloads.Select(GetOverloadModel).ToArray()
                // TODO: attributes
            };
        }

        private OverloadModel GetOverloadModel(IPythonFunctionOverload o) {
            return new OverloadModel {
                Parameters = o.Parameters.Select(p => new ParameterModel {
                    Name = p.Name,
                    Type = GetTypeQualifiedName(p.Type),
                    Kind = GetParameterKind(p.Kind),
                    DefaultValue = GetTypeQualifiedName(p.DefaultValue),
                }).ToArray(),
                ReturnType = GetTypeQualifiedName(o.StaticReturnValue)
            };
        }

        private ClassModel GetClassModel(IPythonClassType cls) {
            return new ClassModel {
                Name = cls.Name,
                Bases = cls.Bases.OfType<IPythonClassType>().Select(GetTypeQualifiedName).ToArray(),
            };
        }

        private static ParameterKind GetParameterKind(Parsing.Ast.ParameterKind p) {
            switch(p) {
                case Parsing.Ast.ParameterKind.KeywordOnly:
                    return ParameterKind.KeywordOnly;
                case Parsing.Ast.ParameterKind.List:
                    return ParameterKind.List;
                case Parsing.Ast.ParameterKind.Dictionary:
                    return ParameterKind.Dictionary;
            }
            return ParameterKind.Normal;
        }

        private string GetVariableCollectionName(string moduleId) => $"{moduleId}.variables";
        private string GetClassesCollectionName(string moduleId) => $"{moduleId}.classes";
        private string GetFunctionsCollectionName(string moduleId) => $"{moduleId}.functions";
        private string GetTypeVarCollectionName(string moduleId) => $"{moduleId}.typeVars";

        // TODO: fix per https://github.com/microsoft/python-language-server/issues/1177
        private string GetModuleUniqueId(IPythonModule module) => module.Name;

        private string GetTypeQualifiedName(IPythonType t) {
            var moduleId = GetModuleUniqueId(t.DeclaringModule);
            switch (t) {
                case IPythonClassMember cm when cm.DeclaringType != null:
                    return $"{moduleId}.{GetClassMemberQualifiedName(cm)}";
                default:
                    return $"{moduleId}.{t.Name}";
            }
        }

        private static string GetClassMemberQualifiedName(IPythonClassMember cm) {
            var s = new Stack<string>();
            s.Push(cm.Name);
            for (var p = cm.DeclaringType as IPythonClassMember; p != null; p = p.DeclaringType as IPythonClassMember) {
                s.Push(p.Name);
            }
            return string.Join(".", s);
        }
    }
}

