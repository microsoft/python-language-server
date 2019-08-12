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
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Extensions {
    public static class PythonPropertyExtensions {
        /// <summary>
        /// Returns true if the decorator combiantions for the property are valid
        /// </summary>
        public static bool HasValidDecorators(this IPythonPropertyType p, IExpressionEvaluator eval) {
            bool valid = true;
            // If property is abstract, allow all decorators because it will be overridden
            if(p.IsAbstract) {
                return valid;
            }

            foreach (var dec in (p.FunctionDefinition?.Decorators?.Decorators).MaybeEnumerate().OfType<NameExpression>()) {
                switch (dec.Name) {
                    case @"staticmethod":
                        ReportInvalidDecorator(dec, Resources.InvalidDecoratorForProperty.FormatInvariant("Staticmethods"), eval);
                        valid = false;
                        break;
                    case @"classmethod":
                        ReportInvalidDecorator(dec, Resources.InvalidDecoratorForProperty.FormatInvariant("Classmethods"), eval);
                        valid = false;
                        break;
                }
            }
            return valid;
        }

        private static void ReportInvalidDecorator(NameExpression name, string errorMsg, IExpressionEvaluator eval) {
            eval.ReportDiagnostics(eval.Module.Uri,
                new DiagnosticsEntry(
                    errorMsg, eval.GetLocation(name).Span,
                    Diagnostics.ErrorCodes.InvalidDecoratorCombination, 
                    Severity.Warning, DiagnosticSource.Analysis
            ));
        }
    }
}
