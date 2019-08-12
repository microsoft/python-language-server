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
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Extensions {
    public static class PythonFunctionExtensions {
        public static bool IsUnbound(this IPythonFunctionType f)
            => f.DeclaringType != null && f.MemberType == PythonMemberType.Function;

        public static bool IsBound(this IPythonFunctionType f)
            => f.DeclaringType != null && f.MemberType == PythonMemberType.Method;

        public static bool HasClassFirstArgument(this IPythonClassMember m)
            => (m is IPythonFunctionType f && !f.IsStatic && (f.IsClassMethod || f.IsBound())) ||
               (m is IPythonPropertyType prop);

        public static IScope GetScope(this IPythonFunctionType f) {
            IScope gs = f.DeclaringModule.GlobalScope;
            return gs?.TraverseBreadthFirst(s => s.Children).FirstOrDefault(s => s.Node == f.FunctionDefinition);
        }

        /// <summary>
        /// Reports any decorator errors on function.
        /// Returns true if the decorator combinations for the property are valid
        /// </summary>
        public static bool HasValidDecorators(this IPythonFunctionType f, IExpressionEvaluator eval) {
            bool valid = true;
            // If function is abstract, allow all decorators because it will be overridden
            if (f.IsAbstract) {
                return valid;
            }

            foreach (var dec in (f.FunctionDefinition?.Decorators?.Decorators).MaybeEnumerate().OfType<NameExpression>()) {
                switch (dec.Name) {
                    case @"staticmethod":
                        if (f.IsClassMethod) {
                            ReportInvalidDecorator(dec, Resources.InvalidDecoratorForFunction.FormatInvariant("Staticmethod", "class"), eval);
                            valid = false;
                        }
                        break;
                    case @"classmethod":
                        if (f.IsStatic) {
                            ReportInvalidDecorator(dec, Resources.InvalidDecoratorForFunction.FormatInvariant("Classmethod", "static"), eval);
                            valid = false;
                        }
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
