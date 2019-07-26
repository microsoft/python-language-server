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
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Caching.Models {
    internal sealed class ModuleModel : MemberModel {
        /// <summary>
        /// Module unique id that includes version.
        /// </summary>
        public string UniqueId { get; set; }

        public string Documentation { get; set; }
        public FunctionModel[] Functions { get; set; }
        public VariableModel[] Variables { get; set; }
        public ClassModel[] Classes { get; set; }

        /// <summary>
        /// Collection of new line information for conversion of linear spans
        /// to line/columns in navigation to member definitions and references.
        /// </summary>
        public NewLineModel[] NewLines { get; set; }

        /// <summary>
        /// Length of the original module file. Used in conversion of indices to line/columns.
        /// </summary>
        public int FileSize { get; set; }

        // TODO: TypeVars, ...

        public static ModuleModel FromAnalysis(IDocumentAnalysis analysis, IServiceContainer services) {
            var variables = new Dictionary<string, VariableModel>();
            var functions = new Dictionary<string, FunctionModel>();
            var classes = new Dictionary<string, ClassModel>();

            // Go directly through variables which names are listed in GetMemberNames
            // as well as variables that are declarations.
            var exportedNames = new HashSet<string>(analysis.Document.GetMemberNames());
            foreach (var v in analysis.GlobalScope.Variables
                .Where(v => exportedNames.Contains(v.Name) || v.Source == VariableSource.Declaration || v.Source == VariableSource.Builtin)) {
                // Create type model before variable since variable needs it.
                string typeName = null;

                switch (v.Value) {
                    case IPythonFunctionType ft when ft.IsLambda():
                        break;
                    case IPythonFunctionType ft
                        when ft.DeclaringModule.Equals(analysis.Document) || ft.DeclaringModule.Equals(analysis.Document.Stub):
                        if (!functions.ContainsKey(ft.Name)) {
                            typeName = ft.Name;
                            functions[ft.Name] = FunctionModel.FromType(ft);
                        }

                        break;
                    case IPythonClassType cls
                        when cls.DeclaringModule.Equals(analysis.Document) || cls.DeclaringModule.Equals(analysis.Document.Stub):
                        if (!classes.ContainsKey(cls.Name)) {
                            typeName = cls.Name;
                            classes[cls.Name] = ClassModel.FromType(cls);
                        }
                        break;
                }

                // Do not re-declare classes and functions as variables in the model.
                if (typeName == null && !variables.ContainsKey(v.Name)) {
                    if (!(v.Value is IPythonFunctionType f && f.IsLambda())) {
                        variables[v.Name] = VariableModel.FromVariable(v);
                    }
                }
            }

            var uniqueId = analysis.Document.GetUniqieId(services);
            return new ModuleModel {
                Id = uniqueId.GetStableHash(),
                UniqueId = uniqueId,
                Name = analysis.Document.Name,
                Documentation = analysis.Document.Documentation,
                Functions = functions.Values.ToArray(),
                Variables = variables.Values.ToArray(),
                Classes = classes.Values.ToArray(),
                NewLines = analysis.Ast.NewLineLocations.Select(l => new NewLineModel {
                    EndIndex = l.EndIndex,
                    Kind = l.Kind
                }).ToArray(),
                FileSize = analysis.Ast.EndIndex
            };
        }
    }
}
