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
using Microsoft.Python.Core;
using Microsoft.PythonTools.Interpreter;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class BuiltinFunctionInfo : BuiltinNamespace<IPythonType>, IHasRichDescription, IHasQualifiedName {
        private string _doc;
        private OverloadResult[] _overloadResults;
        private IPythonFunctionOverload[] _overloads;
        private BuiltinMethodInfo _method;

        public BuiltinFunctionInfo(IPythonFunction function, PythonAnalyzer projectState)
            : base(projectState.Types[BuiltinTypeId.Function], projectState) {
            Function = function;
        }

        public override IPythonType PythonType => Type;

        public override bool IsOfType(IAnalysisSet klass)
            => klass.Contains(ProjectState.ClassInfos[BuiltinTypeId.Function]);

        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            var returnTypes = GetFunctionOverloads().Where(o => o.ReturnType != null).SelectMany(o => o.ReturnType);
            var types = returnTypes.Select(t => {
                var av = ProjectState.GetAnalysisValueFromObjects(t);
                return t.IsTypeFactory
                    ? AnalysisSet.Create(av)
                    : ProjectState.GetAnalysisValueFromObjects(t).GetInstanceType();
            });
            return AnalysisSet.UnionAll(types);
        }

        public override IAnalysisSet GetDescriptor(Node node, AnalysisValue instance, AnalysisValue context, AnalysisUnit unit) {
            if (Function.IsClassMethod) {
                instance = context;
            }

            if (Function.IsStatic || instance.IsOfType(ProjectState.ClassInfos[BuiltinTypeId.NoneType])) {
                return base.GetDescriptor(node, instance, context, unit);
            } else if (_method == null) {
                _method = new BuiltinMethodInfo(Function, PythonMemberType.Method, ProjectState);
            }

            return _method.GetDescriptor(node, instance, context, unit);
        }

        public IPythonFunction Function { get; }
        public override string Name => Function.Name;

        public IEnumerable<KeyValuePair<string, string>> GetRichDescription()
            => GetRichDescription(string.Empty, Function);

        internal static IEnumerable<KeyValuePair<string, string>> GetRichDescription(string def, IPythonFunction function) {
            var needNewline = false;
            foreach (var overload in function.Overloads.OrderByDescending(o => o.GetParameters().Length)) {
                if (needNewline) {
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "\r\n");
                }
                needNewline = true;

                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, def);

                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Name, GetFullName(function.DeclaringType, function.Name));
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "(");
                foreach (var kv in GetParameterString(overload)) {
                    yield return kv;
                }
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, ")");
            }
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.EndOfDeclaration, string.Empty);
        }

        private static string GetFullName(IPythonType type, string name) {
            if (type == null) {
                return name;
            }
            name = type.Name + "." + name;
            if (type.IsBuiltin || type.DeclaringModule == null) {
                return name;
            }
            return type.DeclaringModule.Name + "." + name;
        }

        private static IEnumerable<KeyValuePair<string, string>> GetParameterString(IPythonFunctionOverload overload) {
            var parameters = overload.GetParameters();
            for (int i = 0; i < parameters.Length; i++) {
                if (i != 0) {
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", ");
                }
                var p = parameters[i];

                var name = p.Name;
                if (p.IsKeywordDict) {
                    name = "**" + name;
                } else if (p.IsParamArray) {
                    name = "*" + name;
                }

                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Parameter, name);

                if (!string.IsNullOrWhiteSpace(p.DefaultValue)) {
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, " = ");
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, p.DefaultValue);
                }
            }
        }

        public override IEnumerable<OverloadResult> Overloads {
            get {
                if (_overloadResults == null) {
                    _overloadResults = GetFunctionOverloads()
                        .Select(o => new BuiltinFunctionOverloadResult(ProjectState, Function.Name, o, 0, () => Description))
                        .ToArray();
                }
                return _overloadResults;
            }
        }

        public override string Documentation => _doc ?? (_doc = Utils.StripDocumentation(Function.Documentation));

        public override PythonMemberType MemberType => Function.MemberType;

        public string FullyQualifiedName => FullyQualifiedNamePair.CombineNames();
        public KeyValuePair<string, string> FullyQualifiedNamePair => (Function as IHasQualifiedName)?.FullyQualifiedNamePair ??
            new KeyValuePair<string, string>(DeclaringModule?.ModuleName, Name);

        public override ILocatedMember GetLocatedMember() => Function as ILocatedMember;

        internal override bool UnionEquals(AnalysisValue ns, int strength) {
            if (strength >= MergeStrength.ToObject) {
                return ns is BuiltinFunctionInfo || ns is FunctionInfo || ns == ProjectState.ClassInfos[BuiltinTypeId.Function].Instance;
            }
            return Function.Equals((ns as BuiltinFunctionInfo)?.Function);
        }

        internal override int UnionHashCode(int strength) {
            if (strength >= MergeStrength.ToObject) {
                return ProjectState.ClassInfos[BuiltinTypeId.Function].Instance.UnionHashCode(strength);
            }
            return Function.GetHashCode();
        }

        internal override AnalysisValue UnionMergeTypes(AnalysisValue ns, int strength) {
            if (strength >= MergeStrength.ToObject) {
                return ProjectState.ClassInfos[BuiltinTypeId.Function].Instance;
            }
            if (Function.Equals((ns as BuiltinFunctionInfo)?.Function)) {
                return this;
            }
            return base.UnionMergeTypes(ns, strength);
        }

        public override bool Equals(object obj) {
            if (obj is BuiltinFunctionInfo other && !other.Overloads.SetEquals(Overloads)) {
                return false;
            }
            return base.Equals(obj);
        }

        public override int GetHashCode() => base.GetHashCode() ^ Overloads.GetHashCode();

        private IPythonFunctionOverload[] GetFunctionOverloads() {
            if (_overloads == null) {
                var overloads = Function.Overloads.ToArray();
                // If some of the overloads have return type annotations, drop others.
                // This helps when function has multiple overloads with some definitions
                // comings from the library code and some from the Typeshed. Library code 
                // has documentation but often lacks return types.
                _overloads = overloads.Any(o => !string.IsNullOrEmpty(o.ReturnDocumentation))
                    ? overloads.Where(o => !string.IsNullOrEmpty(o.ReturnDocumentation)).ToArray()
                    : overloads;
            }
            return _overloads;
        }
    }
}
