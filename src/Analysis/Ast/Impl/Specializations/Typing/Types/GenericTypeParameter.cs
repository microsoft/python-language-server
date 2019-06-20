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
        public GenericTypeParameter(string name, IPythonModule declaringModule, IReadOnlyList<IPythonType> constraints, string documentation, IndexSpan location)
            : base(name, new Location(declaringModule), documentation) {
            Constraints = constraints ?? Array.Empty<IPythonType>();
        }
        public IReadOnlyList<IPythonType> Constraints { get; }

        public override BuiltinTypeId TypeId => BuiltinTypeId.Type;
        public override PythonMemberType MemberType => PythonMemberType.Generic;
        public override bool IsSpecialized => true;

        public static IPythonType FromTypeVar(IArgumentSet argSet, IPythonModule declaringModule, IndexSpan location = default) {
            var args = argSet.Values<IMember>();
            var eval = argSet.Eval;

            if (args.Count == 0) {
                var sourceLocation = argSet.CallExpression.GetLocation(eval.Module);

                eval?.ReportDiagnostics(
                    eval.Module.Uri,
                    new DiagnosticsEntry(Resources.TypeVarNoArguments,
                    sourceLocation.Span, 
                    Diagnostics.ErrorCodes.TypeVarNoArguments,
                    Severity.Warning, DiagnosticSource.Analysis)
                );

                return declaringModule.Interpreter.UnknownType;
            }

            var name = (args[0] as IPythonConstant)?.GetString();
            if (string.IsNullOrEmpty(name)) {
                // TODO: report that type name is not a string.
                return declaringModule.Interpreter.UnknownType;
            }

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
