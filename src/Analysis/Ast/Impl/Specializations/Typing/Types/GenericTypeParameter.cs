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
            IReadOnlyList<IPythonType> constraints,
            IPythonType bound,
            IPythonType covariant,
            IPythonType contravariant,
            Location location)
            : base(name, location, GetDocumentation(name, constraints, bound, covariant, contravariant)) {
            Constraints = constraints ?? Array.Empty<IPythonType>();
            Bound = bound;
            Covariant = covariant;
            Contravariant = contravariant;
        }

        #region IGenericTypeParameter
        public IReadOnlyList<IPythonType> Constraints { get; }
        public IPythonType Bound { get; }
        public IPythonType Covariant { get; }
        public IPythonType Contravariant { get; }
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

        public static IPythonType FromTypeVar(IArgumentSet argSet, IPythonModule declaringModule, IndexSpan location = default) {
            if (!TypeVarArgumentsValid(argSet)) {
                return declaringModule.Interpreter.UnknownType;
            }

            var args = argSet.Arguments;
            var constraintArgs = argSet.ListArgument?.Values ?? Array.Empty<IMember>();

            var name = (args[0].Value as IPythonConstant)?.GetString();
            var constraints = constraintArgs.Select(a => {
                // Type constraints may be specified as type name strings.
                var typeString = (a as IPythonConstant)?.GetString();
                return !string.IsNullOrEmpty(typeString) ? argSet.Eval.GetTypeFromString(typeString) : a.GetPythonType();
            }).ToArray();

            var bound = args.Where(a => a.Name == "bound").Select(a => a.Value as IPythonType).FirstOrDefault();
            var covariant = args.Where(a => a.Name == "covariant").Select(a => a.Value as IPythonType).FirstOrDefault();
            var contravariant = args.Where(a => a.Name == "contravariant").Select(a => a.Value as IPythonType).FirstOrDefault();

            return new GenericTypeParameter(name, constraints, bound, covariant, contravariant, new Location(declaringModule, location));
        }

        private static string GetDocumentation(string name, IReadOnlyList<IPythonType> constraints, IPythonType bound, IPythonType covariant, IPythonType contravariant) {
            var constaintStrings = constraints != null ? constraints.Select(c => c.IsUnknown() ? "?" : c.Name) : Enumerable.Empty<string>();
            var boundStrings = bound.IsUnknown() ? Enumerable.Empty<string>() : Enumerable.Repeat($"bound={bound.Name}", 1);
            var covariantStrings = covariant.IsUnknown() ? Enumerable.Empty<string>() : Enumerable.Repeat($"covariant={covariant.Name}", 1);
            var contravariantStrings = contravariant.IsUnknown() ? Enumerable.Empty<string>() : Enumerable.Repeat($"contravariant={contravariant.Name}", 1);

            var docArgs = Enumerable.Repeat($"'{name}'", 1)
                .Concat(constaintStrings).Concat(boundStrings).Concat(covariantStrings).Concat(contravariantStrings);

            var documentation = CodeFormatter.FormatSequence("TypeVar", '(', docArgs);
            return documentation;
        }

        public bool Equals(IGenericTypeParameter other) => Name.Equals(other?.Name);
    }

}
