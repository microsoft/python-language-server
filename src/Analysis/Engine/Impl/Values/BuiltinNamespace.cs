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

using System;
using System.Collections.Generic;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Base class for things which get their members primarily via a built-in .NET type.
    /// </summary>
    class BuiltinNamespace<MemberContainerType> : AnalysisValue where MemberContainerType : IPythonType {
        internal Dictionary<string, IAnalysisSet> _specializedValues;

        public BuiltinNamespace(MemberContainerType pythonType, PythonAnalyzer projectState) {
            ProjectState = projectState ?? throw new ArgumentNullException(nameof(projectState)); ;
            Type = pythonType;
            // Ideally we'd assert here whenever pythonType is null, but that
            // makes debug builds unusable because it happens so often.
        }

        public override BuiltinTypeId TypeId => Type?.TypeId ?? BuiltinTypeId.Unknown;
        public override PythonMemberType MemberType => Type?.MemberType ?? PythonMemberType.Unknown;

        public override IAnalysisSet GetTypeMember(Node node, AnalysisUnit unit, string name) {
            var res = AnalysisSet.Empty;

            if (_specializedValues != null && _specializedValues.TryGetValue(name, out var specializedRes)) {
                return specializedRes;
            }

            if (Type == null) {
                return unit.State.ClassInfos[BuiltinTypeId.NoneType].Instance;
            }

            var member = Type.GetMember(unit.DeclaringModule.InterpreterContext, name);
            if (member != null) {
                res = ProjectState.GetAnalysisValueFromObjects(member);
            }
            return res;
        }

        public override IDictionary<string, IAnalysisSet> GetAllMembers(IModuleContext moduleContext, GetMemberOptions options = GetMemberOptions.None) {
            if (Type == null) {
                return new Dictionary<string, IAnalysisSet>();
            }
            return ProjectState.GetAllMembers(Type, moduleContext);
        }

        public IAnalysisSet this[string name] {
            get {
                if (TryGetMember(name, out var value)) {
                    return value;
                }
                throw new KeyNotFoundException("Key {0} not found".FormatInvariant(name));
            }
            set {
                if (_specializedValues == null) {
                    _specializedValues = new Dictionary<string, IAnalysisSet>();
                }
                _specializedValues[name] = value;
            }
        }

        public bool TryGetMember(string name, out IAnalysisSet value) {
            if (_specializedValues != null && _specializedValues.TryGetValue(name, out var res)) {
                value = res;
                return true;
            }
            if (Type == null) {
                value = null;
                return false;
            }
            var member = Type.GetMember(ProjectState._defaultContext, name);
            if (member != null) {
                value = ProjectState.GetAnalysisValueFromObjects(member);
                return true;
            }
            value = null;
            return false;
        }

        public PythonAnalyzer ProjectState { get; }

        public MemberContainerType Type { get; }

        public virtual ILocatedMember GetLocatedMember() => null;

        public override IEnumerable<ILocationInfo> Locations => GetLocatedMember()?.Locations.MaybeEnumerate();

        public override bool Equals(object obj) {
            if (obj is BuiltinNamespace<MemberContainerType> bn && GetType() == bn.GetType()) {
                return Type != null ? Type.Equals(bn.Type) : bn.Type == null;
            }
            return false;
        }

        public override int GetHashCode() => new { hc1 = GetType().GetHashCode(), hc2 = Type?.GetHashCode() }.GetHashCode();
    }
}
