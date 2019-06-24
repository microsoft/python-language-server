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
    internal sealed class GenericTypeParameter : PythonType, IGenericTypeDefinition {
        private GenericTypeParameter(string name, IPythonModule declaringModule, IReadOnlyList<IPythonType> constraints, string documentation, IndexSpan location)
            : base(name, new Location(declaringModule), documentation) {
            Constraints = constraints ?? Array.Empty<IPythonType>();
        }
        public IReadOnlyList<IPythonType> Constraints { get; }

        public override BuiltinTypeId TypeId => BuiltinTypeId.Type;
        public override PythonMemberType MemberType => PythonMemberType.Generic;
        public override bool IsSpecialized => true;

        private static bool TypeVarArgumentsValid(IArgumentSet argSet) {
            var args = argSet.Values<IMember>();
            var eval = argSet.Eval;
            var callExpression = argSet.CallExpression;
            var callLocation = callExpression?.GetLocation(eval.Module);

            if (args.Count == 0) {
                eval.ReportDiagnostics(
                    eval.Module.Uri,
                    new DiagnosticsEntry(Resources.TypeVarMissingFirstArgument,
                        callLocation?.Span ?? default,
                        Diagnostics.ErrorCodes.TypeVarArguments,
                        Severity.Error, DiagnosticSource.Analysis)
                );

                return false;
            }

            var name = (args[0] as IPythonConstant)?.GetString();
            if (string.IsNullOrEmpty(name)) {
                var firstArgLocation = callExpression?.Args[0]?.GetLocation(eval.Module);
                eval.ReportDiagnostics(
                    eval.Module.Uri,
                    new DiagnosticsEntry(Resources.TypeVarFirstArgumentNotString,
                        firstArgLocation?.Span ?? default,
                        Diagnostics.ErrorCodes.TypeVarArguments,
                        Severity.Warning, DiagnosticSource.Analysis)
                );

                return false;
            }

            // Python gives runtime error when TypeVar has two args
            // e.g. T = TypeVar('T', int)
            if (args.Count == 2) {
                eval.ReportDiagnostics(
                    eval.Module.Uri,
                    new DiagnosticsEntry(Resources.TypeVarSingleConstraint,
                        callLocation?.Span ?? default,
                        Diagnostics.ErrorCodes.TypeVarArguments,
                        Severity.Error, DiagnosticSource.Analysis)
                );
                return false;
            }

            return true;
        }

        public static IPythonType FromTypeVar(IArgumentSet argSet, IPythonModule declaringModule, IndexSpan location = default) {
            if (!TypeVarArgumentsValid(argSet))
                return declaringModule.Interpreter.UnknownType;

            var args = argSet.Values<IMember>();
            var name = (args[0] as IPythonConstant)?.GetString();
            var constraints = args.Skip(1).Select(a => {
                // Type constraints may be specified as type name strings.
                var typeString = (a as IPythonConstant)?.GetString();
                return !string.IsNullOrEmpty(typeString) ? argSet.Eval.GetTypeFromString(typeString) : a.GetPythonType();
            }).ToArray();

            if (constraints.Any(c => c.IsUnknown())) {
                // TODO: report that some constraints could not be resolved. 
            }

            var docArgs = new[] { $"'{name}'" }.Concat(constraints.Select(c => c.IsUnknown() ? "?" : c.Name));
            var documentation = CodeFormatter.FormatSequence("TypeVar", '(', docArgs);

            return new GenericTypeParameter(name, declaringModule, constraints, documentation, location);
        }
    }
}
