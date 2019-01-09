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
using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis.Analyzer.Evaluation {
    /// <summary>
    /// Describes function argument which type is not known from
    /// the function signature and is only known at the call time.
    /// Serves as a placeholder argument type until function return
    /// value can be determined by using actual call arguments.
    /// </summary>
    internal sealed class FunctionArgumentType : PythonTypeWrapper, IFunctionArgumentType, IEquatable<IFunctionArgumentType> {
        public FunctionArgumentType(int parameterIndex, IPythonType parameterType) :
            base(parameterType) {
            ParameterIndex = parameterIndex;
        }

        public int ParameterIndex { get; }

        public override string Name => "function argument";
        public override BuiltinTypeId TypeId => BuiltinTypeId.Type;
        public override PythonMemberType MemberType => PythonMemberType.Variable;
        public bool Equals(IFunctionArgumentType other) => ParameterIndex == other?.ParameterIndex;
    }
}
