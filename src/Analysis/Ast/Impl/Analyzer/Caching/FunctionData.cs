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

namespace Microsoft.Python.Analysis.Analyzer.Caching {
    internal static class FunctionData {
        public static void FromFunction(IPythonFunctionType ft, IDictionary<string, string> md, IScope scope) {
            if (ft.Overloads.Count > 0) {
                md[ft.GetFullyQualifiedName()] = ft.Overloads[0].StaticReturnValue?.GetPythonType()?.Name;
            }

            var functionScope = scope.Children.FirstOrDefault(c => c.Node == ft.FunctionDefinition);
            if (functionScope != null) {
                foreach (var f in functionScope.Variables.Select(v => v.Value as IPythonFunctionType).ExcludeDefault()) {
                    FromFunction(f, md, functionScope);
                }
            }
        }
    }
}
