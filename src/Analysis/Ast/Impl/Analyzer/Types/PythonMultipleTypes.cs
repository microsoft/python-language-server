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

namespace Microsoft.Python.Analysis.Analyzer.Types {
    internal partial class PythonMultipleTypes : IPythonMultipleTypes, ILocatedMember {
        private readonly IPythonType[] _types;
        private readonly object _lock = new object();

        protected PythonMultipleTypes(IPythonType[] types) {
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
            if (member is IPythonMultipleTypes mt) {
                member = Create(mt.Types.Where(m => m is T));
                if (member is T t2) {
                    return t2;
                }
                return mt.Types.OfType<T>().FirstOrDefault();
            }

            return default;
        }

        #region IMemberContainer
        public virtual IEnumerable<string> GetMemberNames() => Types.Select(m => m.Name);
        public virtual IPythonType GetMember(string name) => Types.FirstOrDefault(m => m.Name == name);
        #endregion

        #region ILocatedMember
        public IEnumerable<LocationInfo> Locations
            => Types.OfType<ILocatedMember>().SelectMany(m => m.Locations.MaybeEnumerate());
        #endregion

        #region IPythonType
        public virtual PythonMemberType MemberType => PythonMemberType.Multiple;

        public virtual string Name
            => ChooseName(Types.Select(m => m.Name)) ?? "<multiple>";
        public virtual string Documentation
            => ChooseDocumentation(Types.Select(m => m.Documentation)) ?? string.Empty;
        public virtual bool IsBuiltin
            => Types.Any(m => m.IsBuiltin);
        public virtual IPythonModule DeclaringModule
            => CreateAs<IPythonModule>(Types.Select(f => f.DeclaringModule));
        public virtual BuiltinTypeId TypeId => Types.FirstOrDefault()?.TypeId ?? BuiltinTypeId.Unknown;
        public virtual bool IsTypeFactory => false;
        public virtual IPythonFunction GetConstructor() => null;
        #endregion

        #region Comparison
        // Equality deliberately uses unresolved members
        public override bool Equals(object obj) => GetType() == obj?.GetType() && obj is PythonMultipleTypes mm && new HashSet<IPythonType>(_types).SetEquals(mm._types);
        public override int GetHashCode() => _types.Aggregate(GetType().GetHashCode(), (hc, m) => hc ^ (m?.GetHashCode() ?? 0));
        #endregion

        public IReadOnlyList<IPythonType> Types => _types;

        protected static string ChooseName(IEnumerable<string> names)
            => names.FirstOrDefault(n => !string.IsNullOrEmpty(n));

        protected static string ChooseDocumentation(IEnumerable<string> docs) {
            // TODO: Combine distinct documentation
            return docs.FirstOrDefault(d => !string.IsNullOrEmpty(d));
        }
    }
}
