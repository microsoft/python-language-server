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
using System.Linq;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;

namespace AnalysisTests {
    public static class ModuleAnalysisExtensions {
        public static IEnumerable<string> GetMemberNamesByIndex(this IModuleAnalysis analysis, string exprText, int index, GetMemberOptions options = GetMemberOptions.IntersectMultipleResults) {
            return analysis.GetMembersByIndex(exprText, index, options).Select(m => m.Name);
        }

        public static IEnumerable<IPythonType> GetTypesByIndex(this IModuleAnalysis analysis, string exprText, int index) {
            return analysis.GetValuesByIndex(exprText, index).Select(m => m.PythonType);
        }

        public static IEnumerable<BuiltinTypeId> GetTypeIdsByIndex(this IModuleAnalysis analysis, string exprText, int index) {
            return analysis.GetValuesByIndex(exprText, index).Select(m => {
                if (m.PythonType.TypeId != BuiltinTypeId.Unknown) {
                    return m.PythonType.TypeId;
                }

                var state = analysis.ProjectState;
                if (m == state._noneInst) {
                    return BuiltinTypeId.NoneType;
                }

                var bci = m as BuiltinClassInfo;
                if (bci == null) {
                    var bii = m as BuiltinInstanceInfo;
                    if (bii != null) {
                        bci = bii.ClassInfo;
                    }
                }
                if (bci != null) {
                    int count = (int)BuiltinTypeIdExtensions.LastTypeId;
                    for (int i = 1; i <= count; ++i) {
                        var bti = (BuiltinTypeId)i;
                        if (!bti.IsVirtualId() && analysis.ProjectState.ClassInfos[bti] == bci) {
                            return bti;
                        }
                    }
                }

                return BuiltinTypeId.Unknown;
            });
        }

        public static IEnumerable<string> GetDescriptionsByIndex(this IModuleAnalysis entry, string variable, int index) {
            return entry.GetValuesByIndex(variable, index).Select(m => m.Description);
        }

        public static IEnumerable<string> GetShortDescriptionsByIndex(this IModuleAnalysis entry, string variable, int index) {
            return entry.GetValuesByIndex(variable, index).Select(m => m.ShortDescription);
        }

        public static IEnumerable<string> GetCompletionDocumentationByIndex(this IModuleAnalysis entry, string variable, string memberName, int index) {
            return entry.GetMemberByIndex(variable, memberName, index).Select(m => m.Documentation);
        }

        public static IEnumerable<IMemberResult> GetMemberByIndex(this IModuleAnalysis entry, string variable, string memberName, int index) {
            return entry.GetMembersByIndex(variable, index).Where(m => m.Name == memberName);
        }
    }
}