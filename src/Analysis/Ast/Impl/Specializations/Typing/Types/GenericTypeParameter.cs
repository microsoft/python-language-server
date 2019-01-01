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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Utilities;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Specializations.Typing.Types {
    internal sealed class GenericTypeParameter : PythonType, IGenericTypeParameter {
        public GenericTypeParameter(string name, IPythonModule declaringModule, IReadOnlyList<IPythonType> constraints, string documentation, LocationInfo location)
            : base(name, declaringModule, documentation, location) {
            Constraints = constraints ?? Array.Empty<IPythonType>();
        }
        public IReadOnlyList<IPythonType> Constraints { get; }

        public override BuiltinTypeId TypeId => BuiltinTypeId.Type;
        public override PythonMemberType MemberType => PythonMemberType.Generic;

        public static IPythonType FromTypeVar(IReadOnlyList<IMember> args, IPythonModule declaringModule, LocationInfo location) {
            if (args.Count == 0) {
                // TODO: report that at least one argument is required.
                return declaringModule.Interpreter.UnknownType;
            }

            var name = (args[0] as IPythonConstant)?.Value as string;
            if (string.IsNullOrEmpty(name)) {
                // TODO: report that type name is not a string.
                return declaringModule.Interpreter.UnknownType;
            }

            var constraints = args.Skip(1).Select(a => a.GetPythonType()).ToArray();
            if (constraints.Any(c => c.IsUnknown())) {
                // TODO: report that some constraints could be be resolved.
            }

            var docArgs = new[] { $"'{name}'" }.Concat(constraints.Select(c => c.IsUnknown() ? "?" : c.Name));
            var documentation = CodeFormatter.FormatSequence("TypeVar", '(', docArgs);

            return new GenericTypeParameter(name, declaringModule, constraints, documentation, location);
        }
    }
}
