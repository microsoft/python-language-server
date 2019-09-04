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
using Microsoft.Python.Core.Diagnostics;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.Analysis.Specializations.Typing.Types {
    internal sealed class GenericTypeParameter : PythonType, IGenericTypeParameter {
        public GenericTypeParameter(string name, IPythonModule declaringModule, IReadOnlyList<IPythonType> constraints,
            IPythonType bound, string documentation, IndexSpan location)
            : base(name, new Location(declaringModule), documentation) {
            Constraints = constraints ?? Array.Empty<IPythonType>();
            Bound = bound;
        }

        #region IGenericTypeParameter
        public IReadOnlyList<IPythonType> Constraints { get; }
        public IPythonType Bound { get; }
        #endregion

        public override BuiltinTypeId TypeId => BuiltinTypeId.Type;
        public override PythonMemberType MemberType => PythonMemberType.Generic;
        public override bool IsSpecialized => true;

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
            var boundArg = argSet.Argument("bound");
            // User did not pass in upper bound, bail
            if(boundArg.ValueIsDefault) {
                return null;
            }

            var rawBound = boundArg.Value as IMember;
            switch (rawBound) {
                case IPythonType t:
                    return t;
                case IPythonConstant c when !string.IsNullOrEmpty(c.GetString()):
                    return eval.GetTypeFromString(c.GetString());
                default:
                    return rawBound.GetPythonType();
            }
        }

        public static IPythonType FromTypeVar(IArgumentSet argSet, IPythonModule declaringModule, IndexSpan location = default) {
            Check.ArgumentNotNull(nameof(argSet.Eval), argSet.Eval);

            if (!TypeVarArgumentsValid(argSet)) {
                return declaringModule.Interpreter.UnknownType;
            }

            var args = argSet.Arguments;
            var constraintArgs = argSet.ListArgument?.Values ?? Array.Empty<IMember>();

            var name = argSet.GetArgumentValue<IPythonConstant>("name")?.GetString();
            var constraints = constraintArgs.Select(a => {
                // Type constraints may be specified as type name strings.
                var typeString = a.GetString();
                return !string.IsNullOrEmpty(typeString) ? argSet.Eval.GetTypeFromString(typeString) : a.GetPythonType();
            }).ToArray() ?? Array.Empty<IPythonType>();
            var bound = GetBoundType(argSet);
            var documentation = GetDocumentation(args, constraints);

            return new GenericTypeParameter(name, declaringModule, constraints, bound, documentation, location);
        }

        private static string GetDocumentation(IReadOnlyList<IArgument> args, IReadOnlyList<IPythonType> constraints) {
            var name = (args[0].Value as IPythonConstant).GetString();
            var keyWordArgs = args.Skip(1)
                .Where(x => !x.ValueIsDefault)
                .Select(x => $"{x.Name}={(x.Value as IPythonConstant)?.Value}");

            var docArgs = constraints.Select(c => c.IsUnknown() ? "?" : c.Name).Concat(keyWordArgs).Prepend($"'{name}'");
            var documentation = CodeFormatter.FormatSequence("TypeVar", '(', docArgs);
            return documentation;
        }

        public bool Equals(IGenericTypeParameter other) => Name.Equals(other.Name);
    }
}
