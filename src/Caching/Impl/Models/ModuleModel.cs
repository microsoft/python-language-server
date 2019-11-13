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
using System.Diagnostics;
using System.Linq;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
// ReSharper disable MemberCanBePrivate.Global

namespace Microsoft.Python.Analysis.Caching.Models {
    [Serializable]
    internal sealed class ModuleModel : MemberModel {
        /// <summary>
        /// Module unique id that includes version.
        /// </summary>
        public string UniqueId { get; set; }
        public string FilePath { get; set; }
        public string Documentation { get; set; }
        public FunctionModel[] Functions { get; set; }
        public VariableModel[] Variables { get; set; }
        public ClassModel[] Classes { get; set; }
        public TypeVarModel[] TypeVars { get; set; }
        public NamedTupleModel[] NamedTuples { get; set; }
        //public SubmoduleModel[] SubModules { get; set; }

        /// <summary>
        /// Collection of new line information for conversion of linear spans
        /// to line/columns in navigation to member definitions and references.
        /// </summary>
        public NewLineModel[] NewLines { get; set; }

        /// <summary>
        /// Length of the original module file. Used in conversion of indices to line/columns.
        /// </summary>
        public int FileSize { get; set; }

        [NonSerialized] private Dictionary<string, MemberModel> _modelCache;
        [NonSerialized] private object _modelCacheLock = new object();

        /// <summary>
        /// Constructs module persistent model from analysis.
        /// </summary>
        public static ModuleModel FromAnalysis(IDocumentAnalysis analysis, IServiceContainer services, AnalysisCachingLevel options) {
            var uniqueId = analysis.Document.GetUniqueId(services, options);
            if (uniqueId == null) {
                // Caching level setting does not permit this module to be persisted.
                return null;
            }

            var variables = new Dictionary<string, VariableModel>();
            var functions = new Dictionary<string, FunctionModel>();
            var classes = new Dictionary<string, ClassModel>();
            var typeVars = new Dictionary<string, TypeVarModel>();
            var namedTuples = new Dictionary<string, NamedTupleModel>();
            //var subModules = new Dictionary<string, SubmoduleModel>();

            foreach (var v in analysis.Document.GetMemberNames()
                .Select(x => analysis.GlobalScope.Variables[x]).ExcludeDefault()) {

                if (v.Value is IGenericTypeParameter && !typeVars.ContainsKey(v.Name)) {
                    typeVars[v.Name] = TypeVarModel.FromGeneric(v, services);
                    continue;
                }

                switch (v.Value) {
                    case ITypingNamedTupleType nt:
                        namedTuples[nt.Name] = new NamedTupleModel(nt, services);
                        continue;
                    case IPythonFunctionType ft when ft.IsLambda():
                        // No need to persist lambdas.
                        continue;
                    case IPythonFunctionType ft when v.Name != ft.Name:
                        // Variable assigned to type info of the function like
                        //    def func(): ...
                        //    x = type(func)
                        break;
                    case IPythonFunctionType ft:
                        var fm = GetFunctionModel(analysis, v, ft, services);
                        if (fm != null && !functions.ContainsKey(ft.Name)) {
                            functions[ft.Name] = fm;
                            continue;
                        }
                        break;
                    case IPythonClassType cls when v.Name != cls.Name:
                        // Variable assigned to type info of the class.
                        break;
                    case IPythonClassType cls
                        when cls.DeclaringModule.Equals(analysis.Document) || cls.DeclaringModule.Equals(analysis.Document.Stub):
                        if (!classes.ContainsKey(cls.Name)) {
                            classes[cls.Name] = new ClassModel(cls, services);
                            continue;
                        }
                        break;
                    //case IPythonModule m:
                    //    if (!subModules.ContainsKey(m.Name)) {
                    //        subModules[m.Name] = new SubmoduleModel(m, services);
                    //        continue;
                    //    }
                    //    break;
                }

                // Do not re-declare classes and functions as variables in the model.
                if (!variables.ContainsKey(v.Name)) {
                    variables[v.Name] = VariableModel.FromVariable(v, services);
                }
            }

            return new ModuleModel {
                Id = uniqueId.GetStableHash(),
                UniqueId = uniqueId,
                Name = analysis.Document.Name,
                QualifiedName = analysis.Document.QualifiedName,
                FilePath = analysis.Document.FilePath,
                Documentation = analysis.Document.Documentation,
                Functions = functions.Values.ToArray(),
                Variables = variables.Values.ToArray(),
                Classes = classes.Values.ToArray(),
                TypeVars = typeVars.Values.ToArray(),
                NamedTuples = namedTuples.Values.ToArray(),
                //SubModules = subModules.Values.ToArray(),
                NewLines = analysis.Ast.NewLineLocations.Select(l => new NewLineModel {
                    EndIndex = l.EndIndex,
                    Kind = l.Kind
                }).ToArray(),
                FileSize = analysis.Ast.EndIndex
            };
        }

        private static FunctionModel GetFunctionModel(IDocumentAnalysis analysis, IVariable v, IPythonFunctionType f, IServiceContainer services) {
            if (v.Source == VariableSource.Import && !f.DeclaringModule.Equals(analysis.Document) && !f.DeclaringModule.Equals(analysis.Document.Stub)) {
                // It may be that the function is from a child module via import.
                // For example, a number of functions in 'os' are imported from 'nt' on Windows via
                // star import. Their stubs, however, come from 'os' stub. The function then have declaring
                // module as 'nt' rather than 'os' and 'nt' does not have a stub. In this case use function
                // model like if function was declared in 'os'.
                return new FunctionModel(f, services);
            }

            if (f.DeclaringModule.Equals(analysis.Document) || f.DeclaringModule.Equals(analysis.Document.Stub)) {
                return new FunctionModel(f, services);
            }
            return null;
        }
        
        public override MemberModel GetModel(string name) {
            lock (_modelCacheLock) {
                if (_modelCache == null) {
                    _modelCache = new Dictionary<string, MemberModel>();
                    foreach (var m in GetMemberModels()) {
                        Debug.Assert(!_modelCache.ContainsKey(m.Name));
                        _modelCache[m.Name] = m;
                    }
                }

                return _modelCache.TryGetValue(name, out var model) ? model : null;
            }
        }

        protected override IEnumerable<MemberModel> GetMemberModels() 
            => TypeVars.Concat<MemberModel>(NamedTuples).Concat(Classes).Concat(Functions).Concat(Variables);
    }
}
