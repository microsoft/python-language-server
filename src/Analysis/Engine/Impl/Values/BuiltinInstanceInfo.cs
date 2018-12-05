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
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Interpreter;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class BuiltinInstanceInfo : BuiltinNamespace<IPythonType>, IBuiltinInstanceInfo {
        public BuiltinInstanceInfo(BuiltinClassInfo classInfo)
            : base(classInfo.Type, classInfo.ProjectState) {
            ClassInfo = classInfo;
        }

        IBuiltinClassInfo IBuiltinInstanceInfo.ClassInfo => ClassInfo;
        public BuiltinClassInfo ClassInfo { get; }

        public override string Name => ClassInfo.Name;
        public override IPythonType PythonType => Type;

        public override IAnalysisSet GetInstanceType() {
            if (ClassInfo.TypeId == BuiltinTypeId.Type) {
                return ProjectState.ClassInfos[BuiltinTypeId.Object].Instance;
            }
            return base.GetInstanceType();
        }

        public override string Description => ClassInfo.InstanceDescription;
        public override string ShortDescription => ClassInfo.ShortInstanceDescription;
        public override string Documentation => ClassInfo.Documentation;

        public override PythonMemberType MemberType {
            get {
                switch (ClassInfo.MemberType) {
                    case PythonMemberType.Enum: return PythonMemberType.EnumInstance;
                    default:
                        return PythonMemberType.Instance;
                }
            }
        }

        public override IAnalysisSet GetTypeMember(Node node, AnalysisUnit unit, string name) {
            var res = base.GetTypeMember(node, unit, name);
            if (res.Count > 0) {
                ClassInfo.AddMemberReference(node, unit, name);
                return res.GetDescriptor(node, this, ClassInfo, unit);
            }
            return res;
        }

        public override void SetMember(Node node, AnalysisUnit unit, string name, IAnalysisSet value) {
            var res = base.GetMember(node, unit, name);
            if (res.Count > 0) {
                ClassInfo.AddMemberReference(node, unit, name);
            }
        }

        public override IAnalysisSet UnaryOperation(Node node, AnalysisUnit unit, PythonOperator operation) {
            if (operation == PythonOperator.Not) {
                return unit.State.ClassInfos[BuiltinTypeId.Bool].Instance;
            }

            string methodName = InstanceInfo.UnaryOpToString(unit.State, operation);
            if (methodName != null) {
                var method = GetMember(node, unit, methodName);
                if (method.Count > 0) {
                    var res = method.Call(
                        node,
                        unit,
                        new[] { this },
                        ExpressionEvaluator.EmptyNames
                    );

                    return res;
                }
            }
            return base.UnaryOperation(node, unit, operation);
        }

        public override IAnalysisSet BinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, IAnalysisSet rhs) {
            return ConstantInfo.NumericOp(node, this, unit, operation, rhs) ?? NumericOp(node, unit, operation, rhs) ?? AnalysisSet.Empty;
        }

        private IAnalysisSet NumericOp(Node node, AnalysisUnit unit, PythonOperator operation, IAnalysisSet rhs) {
            string methodName = InstanceInfo.BinaryOpToString(operation);
            if (methodName != null) {
                var method = GetMember(node, unit, methodName);
                if (method.Count > 0) {
                    var res = method.Call(
                        node,
                        unit,
                        new[] { this, rhs },
                        ExpressionEvaluator.EmptyNames
                    );

                    if (res.IsObjectOrUnknown()) {
                        // the type defines the operator, assume it returns 
                        // some combination of the input types.
                        return SelfSet.Union(rhs);
                    }

                    return res;
                }
            }

            return base.BinaryOperation(node, unit, operation, rhs);
        }

        public override IAnalysisSet GetIndex(Node node, AnalysisUnit unit, IAnalysisSet index) {
            var getItem = GetTypeMember(node, unit, "__getitem__");
            if (getItem.Count > 0) {
                var res = getItem.Call(node, unit, new[] { index }, ExpressionEvaluator.EmptyNames);
                if (res.IsObjectOrUnknown() && index.Contains(SliceInfo.Instance)) {
                    // assume slicing returns a type of the same object...
                    return this;
                }
                return res;
            }
            return AnalysisSet.Empty;
        }

        public override IEnumerable<OverloadResult> Overloads {
            get {
                if (ClassInfo.GetAllMembers(ProjectState._defaultContext).TryGetValue("__call__", out var callRes)) {
                    foreach (var overload in callRes.SelectMany(av => av.Overloads)) {
                        yield return overload.WithoutLeadingParameters(1);
                    }
                }

                foreach (var overload in base.Overloads) {
                    yield return overload;
                }
            }
        }

        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            var res = base.Call(node, unit, args, keywordArgNames);

            if (Push()) {
                try {
                    var callRes = GetTypeMember(node, unit, "__call__");
                    if (callRes.Any()) {
                        res = res.Union(callRes.Call(node, unit, args, keywordArgNames));
                    }
                } finally {
                    Pop();
                }
            }

            return res;
        }

        public override IAnalysisSet GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            if (Push()) {
                try {
                    var iter = GetIterator(node, unit);
                    if (iter.Any()) {
                        return iter
                            .GetMember(node, unit, unit.State.LanguageVersion.Is3x() ? "__next__" : "next")
                            .Call(node, unit, ExpressionEvaluator.EmptySets, ExpressionEvaluator.EmptyNames);
                    }
                } finally {
                    Pop();
                }
            }

            return base.GetEnumeratorTypes(node, unit);
        }

        public override IAnalysisSet GetAsyncEnumeratorTypes(Node node, AnalysisUnit unit) {
            if (unit.State.LanguageVersion.Is3x() && Push()) {
                try {
                    var iter = GetAsyncIterator(node, unit);
                    if (iter.Any()) {
                        return iter
                            .GetMember(node, unit, "__anext__")
                            .Call(node, unit, ExpressionEvaluator.EmptySets, ExpressionEvaluator.EmptyNames)
                            .Await(node, unit);
                    }
                } finally {
                    Pop();
                }
            }

            return base.GetAsyncEnumeratorTypes(node, unit);
        }

        public override bool IsOfType(IAnalysisSet classes) {
            if (classes.Contains(ClassInfo)) {
                return true;
            }

            if (TypeId != BuiltinTypeId.NoneType &&
                TypeId != BuiltinTypeId.Type &&
                TypeId != BuiltinTypeId.Function) {
                return classes.Contains(ProjectState.ClassInfos[BuiltinTypeId.Object]);
            }

            return false;
        }

        public override BuiltinTypeId TypeId => ClassInfo?.PythonType.TypeId ?? BuiltinTypeId.Unknown;

        internal override bool UnionEquals(AnalysisValue ns, int strength) {
            var dict = ProjectState.ClassInfos[BuiltinTypeId.Dict];
            if (strength < MergeStrength.IgnoreIterableNode && (this is DictionaryInfo || this == dict.Instance)) {
                if (ns is DictionaryInfo || ns == dict.Instance) {
                    return true;
                }

                if (ns is ConstantInfo ci && ci.ClassInfo == dict) {
                    return true;
                }
                return false;
            }

            if (strength >= MergeStrength.ToObject) {
                if (TypeId == BuiltinTypeId.NoneType || ns.TypeId == BuiltinTypeId.NoneType) {
                    // BII + BII(None) => do not merge
                    // Unless both types are None, since they could be various
                    // combinations of BuiltinInstanceInfo or ConstantInfo that
                    // need to be merged.
                    return TypeId == BuiltinTypeId.NoneType && ns.TypeId == BuiltinTypeId.NoneType;
                }

                var type = ProjectState.ClassInfos[BuiltinTypeId.Type];
                if (ClassInfo == type) {
                    // CI + BII(type) => BII(type)
                    // BCI + BII(type) => BII(type)
                    return ns is ClassInfo || ns is BuiltinClassInfo || ns == type.Instance;
                } else if (ns == type.Instance) {
                    return false;
                }

                if (TypeId == BuiltinTypeId.Function) {
                    // FI + BII(function) => BII(function)
                    return ns is FunctionInfo || ns is BuiltinFunctionInfo ||
                        (ns is BuiltinInstanceInfo && ns.TypeId == BuiltinTypeId.Function);
                }
                if (ns.TypeId == BuiltinTypeId.Function) {
                    return false;
                }

                // BII + II => BII(object)
                // BII + BII(!function) => BII(object)
                return ns is InstanceInfo ||
                    (ns is BuiltinInstanceInfo && ns.TypeId != BuiltinTypeId.Function);

            }

            if (strength >= MergeStrength.ToBaseClass) {
                if (ns is BuiltinInstanceInfo bii) {
                    return ClassInfo.UnionEquals(bii.ClassInfo, strength);
                }

                if (ns is InstanceInfo ii) {
                    return ClassInfo.UnionEquals(ii.ClassInfo, strength);
                }
            } else if (ns is BuiltinInstanceInfo bii) {
                // ConI + BII => BII if CIs match
                return ClassInfo.Equals(bii.ClassInfo);
            }

            return base.UnionEquals(ns, strength);
        }

        internal override int UnionHashCode(int strength) {
            if (strength >= MergeStrength.ToObject) {
                var type = ProjectState.ClassInfos[BuiltinTypeId.Type];
                if (ClassInfo == type) {
                    return type.UnionHashCode(strength);
                }
                return ProjectState.ClassInfos[BuiltinTypeId.Object].GetHashCode();
            }
            return ClassInfo.UnionHashCode(strength);
        }

        internal override AnalysisValue UnionMergeTypes(AnalysisValue ns, int strength) {
            if (strength >= MergeStrength.ToObject) {
                if (TypeId == BuiltinTypeId.NoneType || ns.TypeId == BuiltinTypeId.NoneType) {
                    // BII + BII(None) => do not merge
                    // Unless both types are None, since they could be various
                    // combinations of BuiltinInstanceInfo or ConstantInfo that
                    // need to be merged.
                    return ProjectState.ClassInfos[BuiltinTypeId.NoneType].Instance;
                }

                if (ClassInfo == ProjectState.ClassInfos[BuiltinTypeId.Type]) {
                    return this;
                }

                var func = ProjectState.ClassInfos[BuiltinTypeId.Function];
                if (this == func.Instance) {
                    // FI + BII(function) => BII(function)
                    return func.Instance;
                }

                var type = ProjectState.ClassInfos[BuiltinTypeId.Type];
                if (this == type.Instance) {
                    // CI + BII(type) => BII(type)
                    // BCI + BII(type) => BII(type)
                    return type;
                }

                /// BII + II => BII(object)
                /// BII + BII => BII(object)
                return ProjectState.ClassInfos[BuiltinTypeId.Object].Instance;

            }

            if (strength >= MergeStrength.ToBaseClass) {
                if (ns is BuiltinInstanceInfo bii) {
                    return ClassInfo.UnionMergeTypes(bii.ClassInfo, strength).GetInstanceType().Single();
                }
                if (ns is InstanceInfo ii) {
                    return ClassInfo.UnionMergeTypes(ii.ClassInfo, strength).GetInstanceType().Single();
                }
            } else if (this is ConstantInfo || ns is ConstantInfo) {
                return ClassInfo.Instance;
            }
            return base.UnionMergeTypes(ns, strength);
        }

        #region IReferenceableContainer Members

        public IEnumerable<IReferenceable> GetDefinitions(string name) {
            return ClassInfo.GetDefinitions(name);
        }

        #endregion
    }
}
