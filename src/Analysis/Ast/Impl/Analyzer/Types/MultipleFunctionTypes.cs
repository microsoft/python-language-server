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
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Types {
    /// <summary>
    /// Represent multiple functions that effectively represent a single function
    /// or method, such as when some definitions come from code and some from stubs.
    /// </summary>
    internal sealed class MultipleFunctionTypes : PythonMultipleTypes, IPythonFunction {
        public MultipleFunctionTypes(IPythonType[] members) : base(members) { }

        private IEnumerable<IPythonFunction> Functions => Types.OfType<IPythonFunction>();

        #region IPythonType
        public override PythonMemberType MemberType => PythonMemberType.Function;
        public override string Name => ChooseName(Functions.Select(f => f.Name)) ?? "<function>";
        public override string Documentation => ChooseDocumentation(Functions.Select(f => f.Documentation));
        public override bool IsBuiltin => Functions.Any(f => f.IsBuiltin);
        public override IPythonModule DeclaringModule => CreateAs<IPythonModule>(Functions.Select(f => f.DeclaringModule));
        public override BuiltinTypeId TypeId {
            get {
                if (IsClassMethod) {
                    return BuiltinTypeId.ClassMethod;
                }
                if (IsStatic) {
                    return BuiltinTypeId.StaticMethod;
                }
                return DeclaringType != null ? BuiltinTypeId.Method : BuiltinTypeId.Function;
            }
        }
        #endregion

        #region IPythonFunction
        public bool IsStatic => Functions.Any(f => f.IsStatic);
        public bool IsClassMethod => Functions.Any(f => f.IsClassMethod);
        public IPythonType DeclaringType => CreateAs<IPythonType>(Functions.Select(f => f.DeclaringType));
        public IReadOnlyList<IPythonFunctionOverload> Overloads => Functions.SelectMany(f => f.Overloads).ToArray();
        public FunctionDefinition FunctionDefinition => Functions.FirstOrDefault(f => f.FunctionDefinition != null)?.FunctionDefinition;
        public override IEnumerable<string> GetMemberNames() => Enumerable.Empty<string>();
        #endregion
    }
}
