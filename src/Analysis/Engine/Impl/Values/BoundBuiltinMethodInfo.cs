// Python Tools for Visual Studio
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
using Microsoft.PythonTools.Interpreter;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    class BoundBuiltinMethodInfo : BuiltinNamespace<IPythonType> {
        private OverloadResult[] _overloads;

        public BoundBuiltinMethodInfo(BuiltinMethodInfo method)
            : base(method.PythonType, method.ProjectState) {
            Method = method;
        }

        public BoundBuiltinMethodInfo(IPythonFunction function, PythonAnalyzer projectState)
            : this(new BuiltinMethodInfo(function, PythonMemberType.Method, projectState)) {
        }

        public override PythonMemberType MemberType => Method.MemberType;

        public override BuiltinTypeId TypeId => Method.TypeId;

        public override IPythonType PythonType => Type;

        public BuiltinMethodInfo Method { get; }

        public override string Documentation => Method.Documentation;

        public override string Description => Method.Description;

        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            // Check if method returns self
            var returnType = Method.ReturnTypes.GetInstanceType();
            if (args.Length > 0 && returnType.Split(v => v is BuiltinInstanceInfo biif && biif.PythonType == Method.Function?.DeclaringType, out _, out _)) {
                return args[0]; //  Return actual self (i.e. derived class)
            }
            return returnType;
        }

        public override IEnumerable<OverloadResult> Overloads {
            get {
                if (_overloads == null) {
                    var overloads = Method.Function.Overloads;
                    var result = new OverloadResult[overloads.Count];
                    for (var i = 0; i < result.Length; i++) {
                        result[i] = new BuiltinFunctionOverloadResult(Method.ProjectState, Method.Name, overloads[i], 1);
                    }
                    _overloads = result;
                }
                return _overloads;
            }
        }
    }
}
