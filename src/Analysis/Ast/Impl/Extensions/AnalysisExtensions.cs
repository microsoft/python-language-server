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

using System.Linq;
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core.Text;

namespace Microsoft.Python.Analysis {
    public static class AnalysisExtensions {
        public static IScope FindScope(this IDocumentAnalysis analysis, SourceLocation location)
            => analysis.GlobalScope.FindScope(analysis.Document, location);

        /// <summary>
        /// Provides ability to specialize function return type manually.
        /// </summary>
        public static void SpecializeFunction(this IDocumentAnalysis analysis, string name, IMember returnValue) {
            var f = analysis.GetOrCreateFunction(name);
            if (f != null) {
                foreach (var o in f.Overloads.OfType<PythonFunctionOverload>()) {
                    o.SetReturnValue(returnValue, true);
                }
            }
        }

        /// <summary>
        /// Provides ability to dynamically calculate function return type.
        /// </summary>
        public static void SpecializeFunction(this IDocumentAnalysis analysis, string name, ReturnValueProvider returnTypeCallback, string[] dependencies = null) {
            var f = analysis.GetOrCreateFunction(name);
            if (f != null) {
                foreach (var o in f.Overloads.OfType<PythonFunctionOverload>()) {
                    o.SetReturnValueProvider(returnTypeCallback);
                }
                f.Specialize(dependencies);
            }
        }

        private static PythonFunctionType GetOrCreateFunction(this IDocumentAnalysis analysis, string name) {
            // We DO want to replace class by function. Consider type() in builtins.
            // 'type()' in code is a function call, not a type class instantiation.
            if (!(analysis.GlobalScope.Variables[name]?.Value is PythonFunctionType f)) {
                f = PythonFunctionType.Specialize(name, analysis.Document, string.Empty);
                f.AddOverload(new PythonFunctionOverload(name, new Location(analysis.Document)));
                analysis.GlobalScope.DeclareVariable(name, f, VariableSource.Declaration, new Location(analysis.Document));
            }
            return f;
        }
    }
}
