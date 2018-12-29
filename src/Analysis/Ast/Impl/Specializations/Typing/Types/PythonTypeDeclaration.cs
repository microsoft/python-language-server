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

using System.Collections.Generic;
using System.Linq;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Specializations.Typing.Types {
    /// <summary>
    /// A special callable that declares new type, such as TypeVar.
    /// The difference is that when function is called, it needs
    /// </summary>
    internal sealed class PythonTypeDeclaration : PythonType, IPythonTypeDeclaration {
        public PythonTypeDeclaration(string name, IPythonModuleType declaringModule) :
            base(name, declaringModule, string.Empty, null) { }

        public IPythonType DeclareType(IReadOnlyList<IMember> args, IPythonModuleType declaringModule, string documentation, LocationInfo location) {
            if (args.Count == 0) {
                // TODO: report that at least one argument is required.
                return DeclaringModule.Interpreter.GetBuiltinType(BuiltinTypeId.Unknown);
            }

            var name = (args[0] as IPythonConstant)?.Value as string;
            if (string.IsNullOrEmpty(name)) {
                // TODO: report that type name is not a string.
                return DeclaringModule.Interpreter.GetBuiltinType(BuiltinTypeId.Unknown);
            }

            var constraints = args.Skip(1).ToArray();
            if (constraints.Any(c => (c as IPythonType).IsUnknown())) {
                // TODO: report that some constraints could be be resolved.
            }

            return new GenericTypeParameter(name, declaringModule, 
                constraints.Select(c => c as IPythonType).ToArray(), documentation, location);
        }
    }
}
