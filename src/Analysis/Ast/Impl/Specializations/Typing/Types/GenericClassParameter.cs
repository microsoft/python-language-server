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

namespace Microsoft.Python.Analysis.Specializations.Typing.Types {
    /// <summary>
    /// Represents Generic[T1, T2, ...] parameter. When class is instantiated
    /// or methods evaluated, class generic parameters are matched to
    /// generic type parameters from TypeVar. <see cref="IGenericTypeParameter"/>
    /// </summary>
    internal sealed class GenericClassParameter : PythonClassType, IGenericClassParameter {
        internal GenericClassParameter(IReadOnlyList<IGenericTypeParameter> typeArgs, IPythonInterpreter interpreter)
            : base("Generic", new Location(interpreter.ModuleResolution.GetSpecializedModule("typing"))) {
            TypeParameters = typeArgs ?? Array.Empty<IGenericTypeParameter>();
        }

        #region IPythonType
        public override PythonMemberType MemberType => PythonMemberType.Generic;
        public override string Documentation => Name;
        #endregion

        #region IPythonClassType
        public override bool IsGeneric => true;
        public override IReadOnlyDictionary<IGenericTypeParameter, IPythonType> GenericParameters
            => TypeParameters.ToDictionary(tp => tp, tp => tp as IPythonType ?? UnknownType);
        public override IPythonType CreateSpecificType(IArgumentSet args)
            => new GenericClassParameter(args.Arguments.Select(a => a.Value).OfType<IGenericTypeParameter>().ToArray(), DeclaringModule.Interpreter);
        #endregion

        public IReadOnlyList<IGenericTypeParameter> TypeParameters { get; }

    }
}
