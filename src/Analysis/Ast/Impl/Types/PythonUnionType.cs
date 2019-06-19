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
// permissions and limitations under the License.    }

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Python.Analysis.Utilities;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Diagnostics;

namespace Microsoft.Python.Analysis.Types {
    internal sealed class PythonUnionType : LocatedMember, IPythonUnionType {
        private readonly HashSet<IPythonType> _types = new HashSet<IPythonType>(PythonTypeComparer.Instance);
        private readonly object _lock = new object();

        public PythonUnionType(IEnumerable<IPythonType> types, IPythonModule declaringModule)
            : base(declaringModule.Interpreter.ModuleResolution.GetSpecializedModule("typing")) {
            _types.UnionWith(types);
        }

        private PythonUnionType(IPythonType x, IPythonType y)
            : base(x.DeclaringModule.Interpreter.ModuleResolution.GetSpecializedModule("typing")) {
            Check.Argument(nameof(x), () => !(x is IPythonUnionType));
            Check.Argument(nameof(y), () => !(y is IPythonUnionType));
            _types.Add(x);
            _types.Add(y);
        }

        public override PythonMemberType MemberType => PythonMemberType.Union;

        #region IPythonType

        public string Name {
            get {
                lock (_lock) {
                    return CodeFormatter.FormatSequence("Union", '[', _types.ToArray());
                }
            }
        }

        public string QualifiedName {
            get {
                lock (_lock) {
                    return CodeFormatter.FormatSequence("typing:Union", '[', _types.Select(t => t.QualifiedName).ToArray());
                }
            }
        }

        public BuiltinTypeId TypeId => BuiltinTypeId.Type;
        public string Documentation => Name;

        public bool IsBuiltin {
            get { lock (_lock) { return _types.All(t => t.IsBuiltin); } }
        }

        public bool IsAbstract => false;
        public bool IsSpecialized => true;

        public IMember CreateInstance(string typeName, IArgumentSet args) => new PythonUnion(this);

        public IMember Call(IPythonInstance instance, string memberName, IArgumentSet args) {
            IPythonType[] types;
            lock (_lock) {
                types = _types.ToArray();
            }
            // Check if any types support calls
            var result = types
                .Select(t => t.Call(instance, memberName, args))
                .FirstOrDefault(r => !r.IsUnknown() && r.GetPythonType() != this);
            return result ?? DeclaringModule.Interpreter.UnknownType;
        }

        public IMember Index(IPythonInstance instance, object index) {
            IPythonType[] types;
            lock (_lock) {
                types = _types.ToArray();
            }
            // Check if any types support indexing
            var result = types
                    .Select(t => t.Index(instance, index))
                    .FirstOrDefault(r => !r.IsUnknown() && r.GetPythonType() != this);
            return result ?? DeclaringModule.Interpreter.UnknownType;
        }

        #endregion

        #region IPythonUnionType
        public IPythonUnionType Add(IPythonType t) {
            lock (_lock) {
                if (t is IPythonUnionType ut) {
                    return Add(ut);
                }
                if (!_types.Contains(t)) {
                    _types.Add(t);
                }
                return this;
            }
        }

        public IPythonUnionType Add(IPythonUnionType types) {
            lock (_lock) {
                _types.UnionWith(types);
                return this;
            }
        }

        public IEnumerator<IPythonType> GetEnumerator() {
            lock (_lock) {
                return _types.ToList().GetEnumerator();
            }
        }

        public IMember GetMember(string name) {
            lock (_lock) {
                return _types.Select(t => t.GetMember(name)).ExcludeDefault().FirstOrDefault();
            }
        }

        public IEnumerable<string> GetMemberNames() {
            lock (_lock) {
                return _types.SelectMany(t => t.GetMemberNames()).ToArray();
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        #endregion

        public static IPythonType Combine(IPythonType x, IPythonType y) {
            var utx = x as IPythonUnionType;
            var uty = y as IPythonUnionType;

            if (x == null) {
                return y;
            }
            if (y == null) {
                return x;
            }

            if (utx == null && uty == null) {
                return new PythonUnionType(x, y);
            }

            if (utx != null && uty == null) {
                return utx.Add(y);
            }

            return utx == null ? uty.Add(x) : utx.Add(uty);
        }
    }
}
