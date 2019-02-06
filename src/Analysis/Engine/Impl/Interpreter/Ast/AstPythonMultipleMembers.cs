﻿// Python Tools for Visual Studio
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
using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonMultipleMembers : IPythonMultipleMembers, ILocatedMember {
        private readonly IMember[] _members;
        private IReadOnlyList<IMember> _resolvedMembers;
        private readonly object _lock = new object();

        private AstPythonMultipleMembers(IMember[] members) {
            _members = members ?? Array.Empty<IMember>();
        }

        public static IMember Create(IEnumerable<IMember> members) => Create(members.Where(m => m != null).Distinct().ToArray(), null);

        private static IMember Create(IMember[] members, IMember single) {
            if (single != null && !members.Contains(single)) {
                members = members.Concat(Enumerable.Repeat(single, 1)).ToArray();
            }

            if (members.Length == 1) {
                return members[0];
            }
            if (members.Length == 0) {
                return null;
            }

            if (members.All(m => m is IPythonFunction)) {
                return new MultipleFunctionMembers(members);
            }
            if (members.All(m => m is IPythonModule)) {
                return new MultipleModuleMembers(members);
            }
            if (members.All(m => m is IPythonType)) {
                return new MultipleTypeMembers(members);
            }

            return new AstPythonMultipleMembers(members);
        }

        public static IMember Combine(IMember x, IMember y) {
            if (x == null && y == null) {
                throw new InvalidOperationException("Cannot add two null members");
            }
            if (x == null || (x.MemberType == PythonMemberType.Unknown && !(x is ILazyMember))) {
                return y;
            }
            if (y == null || (y.MemberType == PythonMemberType.Unknown && !(y is ILazyMember))) {
                return x;
            }
            if (x == y) {
                return x;
            }

            var mmx = x as AstPythonMultipleMembers;
            var mmy = y as AstPythonMultipleMembers;

            if (mmx != null && mmy == null) {
                return Create(mmx._members, y);
            }
            if (mmy != null && mmx == null) {
                return Create(mmy._members, x);
            }
            if (mmx != null && mmy != null) {
                return Create(mmx._members.Union(mmy._members).ToArray(), null);
            }
            return Create(new[] { x }, y);
        }

        public static T CreateAs<T>(IEnumerable<IMember> members) => As<T>(Create(members));
        public static T CombineAs<T>(IMember x, IMember y) => As<T>(Combine(x, y));

        public static T As<T>(IMember member) {
            if (member is T t) {
                return t;
            }
            var members = (member as IPythonMultipleMembers)?.GetMembers();
            if (members != null) {
                member = Create(members.Where(m => m is T));
                if (member is T t2) {
                    return t2;
                }
                return members.OfType<T>().FirstOrDefault();
            }

            return default(T);
        }

        #region IMemberContainer
        public IReadOnlyList<IMember> GetMembers() {
            lock (_lock) {
                if (_resolvedMembers != null) {
                    return _resolvedMembers;
                }

                var unresolved = _members.OfType<ILazyMember>().ToArray();
                if (unresolved.Length > 0) {
                    // Publish non-lazy members immediately. This will prevent recursion
                    // into EnsureMembers while we are resolving lazy values.
                    var resolved = _members.Where(m => !(m is ILazyMember)).ToList();
                    _resolvedMembers = resolved.ToArray();

                    foreach (var lm in unresolved) {
                        var m = lm.Get();
                        if (m != null) {
                            resolved.Add(m);
                        }
                    }

                    _resolvedMembers = resolved;
                } else {
                    _resolvedMembers = _members;
                }
                return _resolvedMembers;
            }
        }

        public virtual PythonMemberType MemberType => PythonMemberType.Multiple;
        #endregion

        #region ILocatedMember
        public IEnumerable<ILocationInfo> Locations => GetMembers().OfType<ILocatedMember>().SelectMany(m => m.Locations.MaybeEnumerate());
        #endregion

        #region Comparison
        // Equality deliberately uses unresolved members
        public override bool Equals(object obj) => GetType() == obj?.GetType() && obj is AstPythonMultipleMembers mm && new HashSet<IMember>(_members).SetEquals(mm._members);
        public override int GetHashCode() => _members.Aggregate(GetType().GetHashCode(), (hc, m) => hc ^ (m?.GetHashCode() ?? 0));
        #endregion

        protected static string ChooseName(IEnumerable<string> names) => names.FirstOrDefault(n => !string.IsNullOrEmpty(n));
        protected static string ChooseDocumentation(IEnumerable<string> docs) {
            // TODO: Combine distinct documentation
            return docs.FirstOrDefault(d => !string.IsNullOrEmpty(d));
        }

        /// <summary>
        /// Represent multiple functions that effectively represent a single function
        /// or method, such as when some definitions come from code and some from stubs.
        /// </summary>
        class MultipleFunctionMembers : AstPythonMultipleMembers, IPythonFunction {
            public MultipleFunctionMembers(IMember[] members) : base(members) { }

            private IEnumerable<IPythonFunction> Functions => GetMembers().OfType<IPythonFunction>();

            #region IMember
            public override PythonMemberType MemberType => PythonMemberType.Function;
            #endregion

            #region IPythonType
            public string Name => ChooseName(Functions.Select(f => f.Name)) ?? "<function>";
            public string Documentation => ChooseDocumentation(Functions.Select(f => f.Documentation));
            public bool IsBuiltin => Functions.Any(f => f.IsBuiltin);
            public IPythonModule DeclaringModule => CreateAs<IPythonModule>(Functions.Select(f => f.DeclaringModule));
            public BuiltinTypeId TypeId {
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
            public bool IsTypeFactory => false;
            public IPythonFunction GetConstructor() => null;
            #endregion

            #region IPythonFunction
            public bool IsStatic => Functions.Any(f => f.IsStatic);
            public bool IsClassMethod => Functions.Any(f => f.IsClassMethod);
            public IPythonType DeclaringType => CreateAs<IPythonType>(Functions.Select(f => f.DeclaringType));
            public IReadOnlyList<IPythonFunctionOverload> Overloads => Functions.SelectMany(f => f.Overloads).ToArray();
            public FunctionDefinition FunctionDefinition => Functions.FirstOrDefault(f => f.FunctionDefinition != null)?.FunctionDefinition;
            public IMember GetMember(IModuleContext context, string name) => null;
            public IEnumerable<string> GetMemberNames(IModuleContext moduleContext) => Enumerable.Empty<string>();
            #endregion
        }

        class MultipleModuleMembers : AstPythonMultipleMembers, IPythonModule {
            public MultipleModuleMembers(IMember[] members) : base(members) { }

            private IEnumerable<IPythonModule> Modules => GetMembers().OfType<IPythonModule>();

            public string Name => ChooseName(Modules.Select(m => m.Name)) ?? "<module>";
            public string Documentation => ChooseDocumentation(Modules.Select(m => m.Documentation));
            public IEnumerable<string> GetChildrenModules() => Modules.SelectMany(m => m.GetChildrenModules());
            public IMember GetMember(IModuleContext context, string name) => Create(Modules.Select(m => m.GetMember(context, name)));
            public IEnumerable<string> GetMemberNames(IModuleContext moduleContext) => Modules.SelectMany(m => m.GetMemberNames(moduleContext)).Distinct();
            public override PythonMemberType MemberType => PythonMemberType.Module;

            public IPythonModule DeclaringModule => null;
            public BuiltinTypeId TypeId => BuiltinTypeId.Module;
            public bool IsBuiltin => true;
            public bool IsTypeFactory => false;
            public IPythonFunction GetConstructor() => null;

            public void Imported(IModuleContext context) {
                List<Exception> exceptions = null;
                foreach (var m in Modules) {
                    try {
                        m.Imported(context);
                    } catch (Exception ex) {
                        exceptions = exceptions ?? new List<Exception>();
                        exceptions.Add(ex);
                    }
                }
                if (exceptions != null) {
                    throw new AggregateException(exceptions);
                }
            }
        }

        class MultipleTypeMembers : AstPythonMultipleMembers, IPythonType {
            public MultipleTypeMembers(IMember[] members) : base(members) { }

            private IEnumerable<IPythonType> Types => GetMembers().OfType<IPythonType>();

            public string Name => ChooseName(Types.Select(t => t.Name)) ?? "<type>";
            public string Documentation => ChooseDocumentation(Types.Select(t => t.Documentation));
            public BuiltinTypeId TypeId => Types.GroupBy(t => t.TypeId).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key ?? BuiltinTypeId.Unknown;
            public IPythonModule DeclaringModule => CreateAs<IPythonModule>(Types.Select(t => t.DeclaringModule));
            public bool IsBuiltin => Types.All(t => t.IsBuiltin);
            public bool IsTypeFactory => Types.All(t => t.IsTypeFactory);
            public IMember GetMember(IModuleContext context, string name) => Create(Types.Select(t => t.GetMember(context, name)));
            public IEnumerable<string> GetMemberNames(IModuleContext moduleContext) => Types.SelectMany(t => t.GetMemberNames(moduleContext)).Distinct();
            public override PythonMemberType MemberType => PythonMemberType.Class;
            public IPythonFunction GetConstructor() => CreateAs<IPythonFunction>(Types.Select(t => t.GetConstructor()));
        }
    }
}
