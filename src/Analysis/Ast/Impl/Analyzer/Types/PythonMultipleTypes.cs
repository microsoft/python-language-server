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
using Microsoft.Python.Core;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Types {
    internal class PythonMultipleTypes : IPythonMultipleTypes, ILocatedMember {
        private readonly IPythonType[] _types;
        private readonly object _lock = new object();

        private PythonMultipleTypes(IPythonType[] types) {
            _types = types ?? Array.Empty<IPythonType>();
        }

        public static IPythonType Create(IEnumerable<IPythonType> types) => Create(types.Where(m => m != null).Distinct().ToArray(), null);

        private static IPythonType Create(IPythonType[] types, IPythonType single) {
            if (single != null && !types.Contains(single)) {
                types = types.Concat(Enumerable.Repeat(single, 1)).ToArray();
            }

            if (types.Length == 1) {
                return types[0];
            }
            if (types.Length == 0) {
                return null;
            }

            if (types.All(m => m is IPythonFunction)) {
                return new MultipleFunctionTypes(types);
            }
            if (types.All(m => m is IPythonModule)) {
                return new MultipleModuleTypes(types);
            }
            if (types.All(m => m is IPythonType)) {
                return new MultipleTypeTypes(types);
            }

            return new PythonMultipleTypes(types);
        }

        public static IPythonType Combine(IPythonType x, IPythonType y) {
            if (x == null && y == null) {
                throw new InvalidOperationException("Cannot add two null types");
            }
            if (x == null || (x.MemberType == PythonMemberType.Unknown && !(x is ILazyType))) {
                return y;
            }
            if (y == null || (y.MemberType == PythonMemberType.Unknown && !(y is ILazyType))) {
                return x;
            }
            if (x == y) {
                return x;
            }

            var mmx = x as PythonMultipleTypes;
            var mmy = y as PythonMultipleTypes;

            if (mmx != null && mmy == null) {
                return Create(mmx._types, y);
            }
            if (mmy != null && mmx == null) {
                return Create(mmy._types, x);
            }
            if (mmx != null && mmy != null) {
                return Create(mmx._types.Union(mmy._types).ToArray(), null);
            }
            return Create(new[] { x }, y);
        }

        public static T CreateAs<T>(IEnumerable<IPythonType> types) => As<T>(Create(types));
        public static T CombineAs<T>(IPythonType x, IPythonType y) => As<T>(Combine(x, y));

        public static T As<T>(IPythonType member) {
            if (member is T t) {
                return t;
            }
            var types = (member as IPythonMultipleTypes)?.GetTypes();
            if (types != null) {
                member = Create(types.Where(m => m is T));
                if (member is T t2) {
                    return t2;
                }
                return types.OfType<T>().FirstOrDefault();
            }

            return default;
        }

        #region IMemberContainer
        public virtual IEnumerable<string> GetMemberNames() => GetTypes().Select(m => m.Name);
        public virtual IPythonType GetMember(string name) => GetTypes().FirstOrDefault(m => m.Name == name);
        #endregion

        #region ILocatedMember
        public IEnumerable<LocationInfo> Locations
            => GetTypes().OfType<ILocatedMember>().SelectMany(m => m.Locations.MaybeEnumerate());
        #endregion

        #region IPythonType
        public virtual PythonMemberType MemberType => PythonMemberType.Multiple;

        public virtual string Name
            => ChooseName(GetTypes().Select(m => m.Name)) ?? "<multiple>";
        public virtual string Documentation
            => ChooseDocumentation(GetTypes().Select(m => m.Documentation)) ?? string.Empty;
        public virtual bool IsBuiltin
            => GetTypes().Any(m => m.IsBuiltin);
        public virtual IPythonModule DeclaringModule
            => CreateAs<IPythonModule>(GetTypes().Select(f => f.DeclaringModule));
        public virtual BuiltinTypeId TypeId => GetTypes().FirstOrDefault()?.TypeId ?? BuiltinTypeId.Unknown;
        public virtual bool IsTypeFactory => false;
        public virtual IPythonFunction GetConstructor() => null;
        #endregion

        #region Comparison
        // Equality deliberately uses unresolved members
        public override bool Equals(object obj) => GetType() == obj?.GetType() && obj is PythonMultipleTypes mm && new HashSet<IPythonType>(_types).SetEquals(mm._types);
        public override int GetHashCode() => _types.Aggregate(GetType().GetHashCode(), (hc, m) => hc ^ (m?.GetHashCode() ?? 0));
        #endregion

        public IReadOnlyList<IPythonType> GetTypes() => _types;

        protected static string ChooseName(IEnumerable<string> names)
            => names.FirstOrDefault(n => !string.IsNullOrEmpty(n));

        protected static string ChooseDocumentation(IEnumerable<string> docs) {
            // TODO: Combine distinct documentation
            return docs.FirstOrDefault(d => !string.IsNullOrEmpty(d));
        }

        /// <summary>
        /// Represent multiple functions that effectively represent a single function
        /// or method, such as when some definitions come from code and some from stubs.
        /// </summary>
        private sealed class MultipleFunctionTypes : PythonMultipleTypes, IPythonFunction {
            public MultipleFunctionTypes(IPythonType[] members) : base(members) { }

            private IEnumerable<IPythonFunction> Functions => GetTypes().OfType<IPythonFunction>();

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

        private sealed class MultipleModuleTypes : PythonMultipleTypes, IPythonModule {
            public MultipleModuleTypes(IPythonType[] members) : base(members) { }

            private IEnumerable<IPythonModule> Modules => GetTypes().OfType<IPythonModule>();

            #region IPythonType
            public override PythonMemberType MemberType => PythonMemberType.Module;
            #endregion

            #region IMemberContainer
            public override IPythonType GetMember(string name) => Create(Modules.Select(m => m.GetMember(name)));
            public override IEnumerable<string> GetMemberNames() => Modules.SelectMany(m => m.GetMemberNames()).Distinct();
            #endregion

            #region IPythonType
            public override string Name => ChooseName(Modules.Select(m => m.Name)) ?? "<module>";
            public override string Documentation => ChooseDocumentation(Modules.Select(m => m.Documentation));
            public override IPythonModule DeclaringModule => null;
            public override BuiltinTypeId TypeId => BuiltinTypeId.Module;
            public override bool IsBuiltin => true;
            #endregion

            #region IPythonModule
            public IEnumerable<string> GetChildrenModuleNames() => Modules.SelectMany(m => m.GetChildrenModuleNames());
            public void LoadAndAnalyze() {
                List<Exception> exceptions = null;
                foreach (var m in Modules) {
                    try {
                        m.LoadAndAnalyze();
                    } catch (Exception ex) {
                        exceptions = exceptions ?? new List<Exception>();
                        exceptions.Add(ex);
                    }
                }
                if (exceptions != null) {
                    throw new AggregateException(exceptions);
                }
            }
            public IEnumerable<string> ParseErrors { get; private set; } = Enumerable.Empty<string>();
            #endregion

            #region IPythonFile
            public string FilePath => null;
            public Uri Uri => null;
            public IPythonInterpreter Interpreter => null;
            #endregion
        }

        class MultipleTypeTypes : PythonMultipleTypes, IPythonType {
            public MultipleTypeTypes(IPythonType[] members) : base(members) { }

            private IEnumerable<IPythonType> Types => GetTypes().OfType<IPythonType>();

            public override string Name => ChooseName(Types.Select(t => t.Name)) ?? "<type>";
            public override string Documentation => ChooseDocumentation(Types.Select(t => t.Documentation));
            public override BuiltinTypeId TypeId => Types.GroupBy(t => t.TypeId).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key ?? BuiltinTypeId.Unknown;
            public override IPythonModule DeclaringModule => CreateAs<IPythonModule>(Types.Select(t => t.DeclaringModule));
            public override bool IsBuiltin => Types.All(t => t.IsBuiltin);
            public override bool IsTypeFactory => Types.All(t => t.IsTypeFactory);
            public override IPythonType GetMember(string name) => Create(Types.Select(t => t.GetMember(name)));
            public override IEnumerable<string> GetMemberNames() => Types.SelectMany(t => t.GetMemberNames()).Distinct();
            public override PythonMemberType MemberType => PythonMemberType.Class;
            public override IPythonFunction GetConstructor() => CreateAs<IPythonFunction>(Types.Select(t => t.GetConstructor()));
        }
    }
}
