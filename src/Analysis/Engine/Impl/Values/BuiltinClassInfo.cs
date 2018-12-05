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
using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class BuiltinClassInfo : BuiltinNamespace<IPythonType>, IBuiltinClassInfo {
        private BuiltinInstanceInfo _inst;
        private string _doc;
        private readonly MemberReferences _referencedMembers = new MemberReferences();
        private ReferenceDict _references;

        internal static string[] EmptyStrings = new string[0];

        public BuiltinClassInfo(IPythonType classObj, PythonAnalyzer projectState)
            : base(classObj, projectState) {
            // TODO: Get parameters from ctor
            // TODO: All types should be shared via projectState
            _doc = null;
        }

        public override IPythonType PythonType => Type;
        public override bool IsOfType(IAnalysisSet klass)
            => klass.Contains(ProjectState.ClassInfos[BuiltinTypeId.Type]);

        public override BuiltinTypeId TypeId => Type.TypeId;

        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            // TODO: More Type propagation
            IAdvancedPythonType advType = Type as IAdvancedPythonType;
            if (advType != null) {
                var types = advType.GetTypesPropagatedOnCall();
                if (types != null) {
                    IAnalysisSet[] propagating = new IAnalysisSet[types.Count];
                    for (int i = 0; i < propagating.Length; i++) {
                        propagating[i] = unit.State.GetInstance(types[i]).SelfSet;
                    }
                    foreach (var arg in args) {
                        arg.Call(node, unit, propagating, ExpressionEvaluator.EmptyNames);
                    }
                }
            }

            return Instance.SelfSet;
        }

        public override string Name => Type?.Name;
        public string InstanceDescription {
            get {
                switch (TypeId) {
                    case BuiltinTypeId.NoneType:
                        return "None";
                }
                return Type?.Name ?? "<unknown>";
            }
        }

        public string ShortInstanceDescription => InstanceDescription;

        public string FullyQualifiedName {
            get {
                if (Type != null) {
                    if (Type.IsBuiltin) {
                        return Type.Name;
                    }
                    return Type.DeclaringModule.Name + "." + Type.Name;
                }
                return null;
            }
        }

        public KeyValuePair<string, string> FullyQualifiedNamePair {
            get {
                if (Type != null) {
                    return new KeyValuePair<string, string>(Type.DeclaringModule.Name, Type.Name);
                }
                throw new NotSupportedException();
            }
        }

        public override IMro Mro {
            get {
                var mro = (Type as IPythonClass)?.Mro;
                if (mro != null) {
                    return new Mro(mro.Where(t => t != null).Select(t => ProjectState.GetBuiltinType(t)));
                }
                return Values.Mro.Empty;
            }
        }

        IBuiltinInstanceInfo IBuiltinClassInfo.Instance => Instance;
        public BuiltinInstanceInfo Instance => _inst ?? (_inst = MakeInstance());
        public override IAnalysisSet GetInstanceType() => Instance;

        protected virtual BuiltinInstanceInfo MakeInstance() {
            if (Type.TypeId == BuiltinTypeId.Int || Type.TypeId == BuiltinTypeId.Long || Type.TypeId == BuiltinTypeId.Float || Type.TypeId == BuiltinTypeId.Complex) {
                return new NumericInstanceInfo(this);
            } else if (Type.TypeId == BuiltinTypeId.Str || Type.TypeId == BuiltinTypeId.Unicode || Type.TypeId == BuiltinTypeId.Bytes) {
                return new SequenceBuiltinInstanceInfo(this, true, true);
            } else if (Type.TypeId == BuiltinTypeId.Tuple || Type.TypeId == BuiltinTypeId.List) {
                Debug.Fail("Overloads should have been called here");
                // But we fall back to the old type anyway
                return new SequenceBuiltinInstanceInfo(this, false, false);
            }

            return new BuiltinInstanceInfo(this);
        }

        public override IEnumerable<OverloadResult> Overloads {
            get {
                // TODO: sometimes might have a specialized __init__.
                // This just covers typical .NET types
                var ctors = Type.GetConstructor();
                return ctors != null
                    ? ctors.Overloads.Select(ctor => new BuiltinFunctionOverloadResult(ProjectState, Type.Name, ctor, 1, () => Documentation))
                    : Enumerable.Empty<OverloadResult>();
            }
        }

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            // Must unconditionally call the base implementation of GetMember
            var res = base.GetMember(node, unit, name);
            if (res.Count > 0) {
                _referencedMembers.AddReference(node, unit, name);
                return res.GetDescriptor(node, unit.State._noneInst, this, unit);
            }
            return res;
        }

        public override void SetMember(Node node, AnalysisUnit unit, string name, IAnalysisSet value) {
            base.SetMember(node, unit, name, value);
            _referencedMembers.AddReference(node, unit, name);
        }

        public override IAnalysisSet GetIndex(Node node, AnalysisUnit unit, IAnalysisSet index) {
            var clrType = Type as IAdvancedPythonType;
            if (clrType == null || !clrType.IsGenericTypeDefinition) {
                return AnalysisSet.Empty;
            }

            var result = AnalysisSet.Create();
            foreach (var indexType in index) {
                if (indexType is BuiltinClassInfo) {
                    var clrIndexType = indexType.PythonType;
                    try {
                        var klass = ProjectState.MakeGenericType(clrType, clrIndexType);
                        result = result.Add(klass);
                    } catch {
                        // wrong number of type args, violated constraint, etc...
                    }
                } else if (indexType is SequenceInfo) {
                    List<IPythonType>[] types = GetSequenceTypes(indexType as SequenceInfo);

                    if (!MissingType(types)) {
                        foreach (IPythonType[] indexTypes in GetTypeCombinations(types)) {
                            try {
                                var klass = ProjectState.MakeGenericType(clrType, indexTypes);
                                result = result.Add(klass);
                            } catch {
                                // wrong number of type args, violated constraint, etc...
                            }
                        }
                    }
                }
            }
            return result;
        }

        private static IEnumerable<IPythonType[]> GetTypeCombinations(List<IPythonType>[] types) {
            List<IPythonType> res = new List<IPythonType>();
            for (int i = 0; i < types.Length; i++) {
                res.Add(null);
            }

            return GetTypeCombinationsWorker(types, res, 0);
        }

        private static IEnumerable<IPythonType[]> GetTypeCombinationsWorker(List<IPythonType>[] types, List<IPythonType> res, int curIndex) {
            if (curIndex == types.Length) {
                yield return res.ToArray();
            } else {
                foreach (IPythonType t in types[curIndex]) {
                    res[curIndex] = t;

                    foreach (var finalRes in GetTypeCombinationsWorker(types, res, curIndex + 1)) {
                        yield return finalRes;
                    }
                }
            }
        }

        private static List<IPythonType>[] GetSequenceTypes(SequenceInfo seq) {
            List<IPythonType>[] types = new List<IPythonType>[seq.IndexTypes.Length];
            for (int i = 0; i < types.Length; i++) {
                foreach (var seqIndexType in seq.IndexTypes[i].Types) {
                    if (seqIndexType is BuiltinClassInfo) {
                        if (types[i] == null) {
                            types[i] = new List<IPythonType>();
                        }

                        types[i].Add(seqIndexType.PythonType);
                    }
                }
            }
            return types;
        }

        private static bool MissingType(List<IPythonType>[] types) {
            for (int i = 0; i < types.Length; i++) {
                if (types[i] == null) {
                    return true;
                }
            }
            return false;
        }

        public virtual IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, Type.IsBuiltin ? "type " : "class ");
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Name, FullName);
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.EndOfDeclaration, string.Empty);
        }

        private string FullName {
            get {
                var name = Type.Name;
                if (!Type.IsBuiltin && !string.IsNullOrEmpty(Type.DeclaringModule?.Name)) {
                    name = Type.DeclaringModule.Name + "." + name;
                }
                return name;
            }
        }

        public override string Documentation {
            get {
                if (_doc == null) {
                    try {
                        var doc = Type.Documentation ?? string.Empty;
                        _doc = Utils.StripDocumentation(doc.ToString());
                    } catch {
                        _doc = String.Empty;
                    }
                }
                return _doc;
            }
        }

        public override PythonMemberType MemberType => Type.MemberType;
        public override string ToString() => "Class " + Type.Name;

        internal override AnalysisValue UnionMergeTypes(AnalysisValue ns, int strength) {
            if (strength >= MergeStrength.ToObject) {
                AnalysisValue type;
                if (TypeId == ns.TypeId && (type = ProjectState.ClassInfos[TypeId]) != null) {
                    return type;
                }
                return ProjectState.ClassInfos[BuiltinTypeId.Type];

            } else if (strength >= MergeStrength.ToBaseClass) {
                var commonBase = ClassInfo.GetFirstCommonBase(ProjectState, this, ns);
                if (commonBase != null) {
                    return commonBase;
                }

                return ProjectState.ClassInfos[BuiltinTypeId.Object];
            }

            return base.UnionMergeTypes(ns, strength);
        }

        internal override bool UnionEquals(AnalysisValue ns, int strength) {
            if (strength >= MergeStrength.ToObject) {
                var type = ProjectState.ClassInfos[BuiltinTypeId.Type];
                return ns is ClassInfo || ns is BuiltinClassInfo || ns == type || ns == type.Instance;

            } else if (strength >= MergeStrength.ToBaseClass) {
                return ClassInfo.GetFirstCommonBase(ProjectState, this, ns) != null;
            }

            return base.UnionEquals(ns, strength);
        }

        internal override int UnionHashCode(int strength) {
            if (strength >= MergeStrength.ToBaseClass) {
                return ProjectState.ClassInfos[BuiltinTypeId.Type].GetHashCode();
            } else {
                return base.UnionHashCode(strength);
            }
        }

        #region IReferenceableContainer Members

        public IEnumerable<IReferenceable> GetDefinitions(string name) {
            return _referencedMembers.GetDefinitions(name, PythonType, ProjectState._defaultContext);
        }

        #endregion

        internal void AddMemberReference(Node node, AnalysisUnit unit, string name) {
            _referencedMembers.AddReference(node, unit, name);
        }

        internal override void AddReference(Node node, AnalysisUnit unit) {
            if (!unit.ForEval) {
                if (_references == null) {
                    _references = new ReferenceDict();
                }
                _references.GetReferences(unit.DeclaringModule.ProjectEntry as ProjectEntry)?.AddReference(new EncodedLocation(unit, node));
            }
        }

        internal override IEnumerable<ILocationInfo> References => _references?.AllReferences ?? new LocationInfo[0];
        public override ILocatedMember GetLocatedMember() => Type as ILocatedMember;
    }
}
