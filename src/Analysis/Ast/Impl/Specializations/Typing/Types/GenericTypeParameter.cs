﻿// Copyright(c) Microsoft Corporation
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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Utilities;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core.Text;

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
            var args = argSet.Arguments;
            var constraintArgs = argSet.ListArgument?.Values;

            if (args.Count == 0) {
                // TODO: report that at least one argument is required.
                return declaringModule.Interpreter.UnknownType;
            }

            var name = (args[0].Value as IPythonConstant)?.GetString();
            if (string.IsNullOrEmpty(name)) {
                // TODO: report that type name is not a string.
                return declaringModule.Interpreter.UnknownType;
            }

            var constraints = constraintArgs?.Select(a => {
                // Type constraints may be specified as type name strings.
                var typeString = (a as IPythonConstant)?.GetString();
                return !string.IsNullOrEmpty(typeString) ? argSet.Eval.GetTypeFromString(typeString) : a.GetPythonType();
            }).ToArray() ?? Array.Empty<IPythonType>();

            if (constraints.Any(c => c.IsUnknown())) {
                // TODO: report that some constraints could not be resolved.
            }

            var documentation = GetDocumentation(args, constraints);
            return new GenericTypeParameter(name, declaringModule, constraints, documentation, location);
        }

        private static string GetDocumentation(IReadOnlyList<IArgument> args, IReadOnlyList<IPythonType> constraints) {
            var name = (args[0].Value as IPythonConstant).GetString();
            var keyWordArgs = new List<string>();

            foreach (var arg in args.Skip(1)) {
                if (arg.ValueIsDefault)
                    continue;

                keyWordArgs.Add($"{arg.Name}={(arg.Value as IPythonConstant)?.Value}");
            }

            var docArgs = constraints.Select(c => c.IsUnknown() ? "?" : c.Name).Concat(keyWordArgs).Prepend($"'{name}'");
            var documentation = CodeFormatter.FormatSequence("TypeVar", '(', docArgs);
            return documentation;
        }
    }
}
