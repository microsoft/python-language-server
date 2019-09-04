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
using System.Linq;
using Microsoft.Python.Analysis.Diagnostics;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Utilities;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.Analysis.Specializations.Typing.Types {
    internal sealed class GenericTypeParameter : PythonType, IGenericTypeParameter {
        public GenericTypeParameter(
            string name,
            IPythonModule declaringModule,
            IReadOnlyList<IPythonType> constraints,
            IPythonType bound,
            object covariant,
            object contravariant,
            IndexSpan indexSpan)
            : base(name, new Location(declaringModule, indexSpan),
                GetDocumentation(name, constraints, bound, covariant, contravariant, declaringModule)) {
            Constraints = constraints ?? Array.Empty<IPythonType>();
            Bound = bound;
            Covariant = covariant;
            Contravariant = contravariant;
        }

        #region IGenericTypeParameter
        public IReadOnlyList<IPythonType> Constraints { get; }
        public IPythonType Bound { get; }
        public object Covariant { get; }
        public object Contravariant { get; }
        #endregion

        #region IPythonType

        public override BuiltinTypeId TypeId => BuiltinTypeId.Type;
        public override PythonMemberType MemberType => PythonMemberType.Generic;
        public override bool IsSpecialized => true;

        #endregion

        private static bool TypeVarArgumentsValid(IArgumentSet argSet) {
            var args = argSet.Arguments;
            var constraints = argSet.ListArgument?.Values ?? Array.Empty<IMember>();

            var eval = argSet.Eval;
            var expr = argSet.Expression;
            var callLocation = expr?.GetLocation(eval);

            if (argSet.Errors.Count > 0) {
                argSet.ReportErrors();
                return false;
            }

            // Report diagnostic if user passed in a value for name and it is not a string
            var name = (args[0].Value as IPythonConstant)?.GetString();
            if (string.IsNullOrEmpty(name)) {
                eval.ReportDiagnostics(
                    eval.Module.Uri,
                    new DiagnosticsEntry(Resources.TypeVarFirstArgumentNotString,
                        callLocation?.Span ?? default,
                        Diagnostics.ErrorCodes.TypingTypeVarArguments,
                        Severity.Warning, DiagnosticSource.Analysis)
                );
                return false;
            }

            // Python gives runtime error when TypeVar has one constraint
            // e.g. T = TypeVar('T', int)
            if (constraints.Count == 1) {
                eval.ReportDiagnostics(
                    eval.Module.Uri,
                    new DiagnosticsEntry(Resources.TypeVarSingleConstraint,
                        callLocation?.Span ?? default,
                        Diagnostics.ErrorCodes.TypingTypeVarArguments,
                        Severity.Warning, DiagnosticSource.Analysis)
                );
                return false;
            }

            return true;
        }

        /// <summary>
        /// Given arguments to TypeVar, finds the bound type
        /// </summary>
        private static IPythonType GetBoundType(IArgumentSet argSet) {
            var eval = argSet.Eval;
            var rawBound = argSet.GetArgumentValue<IMember>("bound");
            switch (rawBound) {
                case IPythonType t:
                    return t;
                case IPythonConstant c:
                    var s = c.GetString();
                    if (!string.IsNullOrEmpty(s)) {
                        return eval.GetTypeFromString(s) ?? argSet.Eval.UnknownType;
                    }
                    return argSet.Eval.UnknownType;
                default:
                    return rawBound.GetPythonType();
            }
        }

        public static IPythonType FromTypeVar(IArgumentSet argSet, IPythonModule declaringModule, IndexSpan indexSpan = default) {
            if (!TypeVarArgumentsValid(argSet)) {
                return declaringModule.Interpreter.UnknownType;
            }

            var constraintArgs = argSet.ListArgument?.Values ?? Array.Empty<IMember>();

            var name = argSet.GetArgumentValue<IPythonConstant>("name")?.GetString();
            var constraints = constraintArgs.Select(a => {
                // Type constraints may be specified as type name strings.
                var typeString = a.GetString();
                return !string.IsNullOrEmpty(typeString) ? argSet.Eval.GetTypeFromString(typeString) : a.GetPythonType();
            }).ToArray();

            var bound = GetBoundType(argSet);
            var covariant = argSet.GetArgumentValue<IPythonConstant>("covariant")?.Value;
            var contravariant = argSet.GetArgumentValue<IPythonConstant>("contravariant")?.Value;

            return new GenericTypeParameter(name, declaringModule, constraints, bound, covariant, contravariant, indexSpan);
        }

        private static string GetDocumentation(string name, IReadOnlyList<IPythonType> constraints, IPythonType bound, object covariant, object contravariant, IPythonModule declaringModule) {
            var constaintStrings = constraints != null ? constraints.Select(c => c.IsUnknown() ? "?" : c.Name) : Enumerable.Empty<string>();

            var boundStrings = Enumerable.Empty<string>();
            if (bound != null) {
                var boundName = bound.DeclaringModule.Equals(declaringModule)
                    ? bound.Name : $"{bound.DeclaringModule}.{bound.Name}";
                boundStrings = Enumerable.Repeat($"bound={boundName}", 1);
            }

            var covariantStrings = covariant != null ? Enumerable.Repeat($"covariant={covariant}", 1) : Enumerable.Empty<string>();
            var contravariantStrings = contravariant != null ? Enumerable.Repeat($"contravariant={contravariant}", 1) : Enumerable.Empty<string>();

            var docArgs = Enumerable.Repeat($"'{name}'", 1)
                .Concat(constaintStrings).Concat(boundStrings).Concat(covariantStrings).Concat(contravariantStrings);

            var documentation = CodeFormatter.FormatSequence("TypeVar", '(', docArgs);
            return documentation;
        }

        public bool Equals(IGenericTypeParameter other) => Name.Equals(other?.Name);
    }
}
