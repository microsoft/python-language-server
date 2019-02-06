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
using System.Linq;
using System.Text;
using Microsoft.PythonTools.Interpreter;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class BuiltinMethodInfo : BuiltinNamespace<IPythonType>, IHasRichDescription {
        private string _doc;
        private BoundBuiltinMethodInfo _boundMethod;

        public BuiltinMethodInfo(IPythonFunction function, PythonMemberType memType, PythonAnalyzer projectState)
            : base(projectState.Types[
                    memType == PythonMemberType.Function ? BuiltinTypeId.Function : BuiltinTypeId.Method
                ], projectState) {
            MemberType = memType;
            Function = function;
            ReturnTypes = GetReturnTypes(function, projectState);
        }

        public override IPythonType PythonType => Type;

        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames)
            => ReturnTypes.GetInstanceType();

        public override IAnalysisSet GetDescriptor(Node node, AnalysisValue instance, AnalysisValue context, AnalysisUnit unit) {
            if (instance == ProjectState._noneInst) {
                return base.GetDescriptor(node, instance, context, unit);
            }

            _boundMethod = _boundMethod ?? new BoundBuiltinMethodInfo(this);
            return _boundMethod.SelfSet;
        }

        public IEnumerable<KeyValuePair<string, string>> GetRichDescription()
            => BuiltinFunctionInfo.GetRichDescription(string.Empty, Function);

        public IAnalysisSet ReturnTypes { get; }
        public IPythonFunction Function { get; }

        public override IEnumerable<OverloadResult> Overloads
            => Function.Overloads.Select(overload =>
                    new BuiltinFunctionOverloadResult(
                        ProjectState,
                        Function.Name,
                        overload,
                        0,
                        new ParameterResult("self")
                    )
                );

        public override string Documentation {
            get {
                if (_doc == null) {
                    var doc = new StringBuilder();
                    foreach (var overload in Function.Overloads) {
                        doc.Append(Utils.StripDocumentation(overload.Documentation));
                    }
                    _doc = doc.ToString();
                    if (string.IsNullOrWhiteSpace(_doc)) {
                        _doc = Utils.StripDocumentation(Function.Documentation);
                    }
                }
                return _doc;
            }
        }
        public override BuiltinTypeId TypeId => BuiltinTypeId.Function;
        public override PythonMemberType MemberType { get; }
        public override string Name => Function.Name;
        public override ILocatedMember GetLocatedMember() => Function as ILocatedMember;

        public override int GetHashCode() => new { hc1 = base.GetHashCode(), hc2 = Function.GetHashCode() }.GetHashCode();
        public override bool Equals(object obj) => base.Equals(obj) && obj is BuiltinMethodInfo bmi && Function.Equals(bmi.Function);

        private IAnalysisSet GetReturnTypes(IPythonFunction func, PythonAnalyzer projectState)
            => AnalysisSet.UnionAll(func.Overloads
                .Where(fn => fn.ReturnType != null)
                .Select(fn => projectState.GetAnalysisSetFromObjects(fn.ReturnType)));
    }
}
