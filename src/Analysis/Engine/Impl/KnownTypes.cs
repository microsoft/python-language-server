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
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Ast;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Analysis {
    internal interface IKnownPythonTypes {
        IPythonType this[BuiltinTypeId id] { get; }
    }

    internal interface IKnownClasses {
        BuiltinClassInfo this[BuiltinTypeId id] { get; }
    }

    internal class KnownTypes : IKnownPythonTypes, IKnownClasses {
        internal readonly IPythonType[] _types;
        internal readonly BuiltinClassInfo[] _classInfos;

        public static KnownTypes CreateDefault(PythonAnalyzer state, IBuiltinPythonModule fallback) {
            var res = new KnownTypes();

            for (var value = 0; value < res._types.Length; ++value) {
                res._types[value] = (IPythonType)fallback.GetAnyMember(
                    ((BuiltinTypeId)value).GetTypeName(state.LanguageVersion)
                );
                Debug.Assert(res._types[value] != null);
            }

            res.SetClassInfo(state);
            return res;
        }

        public static KnownTypes Create(PythonAnalyzer state, IBuiltinPythonModule fallback) {
            var res = new KnownTypes();

            var interpreter = state.Interpreter;

            for (var value = 0; value < res._types.Length; ++value) {
                IPythonType type;
                try {
                    type = interpreter.GetBuiltinType((BuiltinTypeId)value);
                } catch (KeyNotFoundException) {
                    type = null;
                }
                if (type == null) {
                    type = (IPythonType)fallback.GetAnyMember(((BuiltinTypeId)value).GetTypeName(state.LanguageVersion));
                    Debug.Assert(type != null);
                }
                res._types[value] = type;
            }

            res.SetClassInfo(state);
            return res;
        }

        private KnownTypes() {
            var count = (int)BuiltinTypeIdExtensions.LastTypeId + 1;
            _types = new IPythonType[count];
            _classInfos = new BuiltinClassInfo[count];
        }

        private void SetClassInfo(PythonAnalyzer state) {
            for (var value = 0; value < _types.Length; ++value) {
                if (_types[value] != null) {
                    _classInfos[value] = state.GetBuiltinType(_types[value]);
                }
            }
        }

        IPythonType IKnownPythonTypes.this[BuiltinTypeId id] => _types[(int)id];

        BuiltinClassInfo IKnownClasses.this[BuiltinTypeId id] => _classInfos[(int)id];
    }

    class FallbackBuiltinModule : PythonModuleType, IBuiltinPythonModule {
        public readonly PythonLanguageVersion LanguageVersion;
        private readonly Dictionary<BuiltinTypeId, IMember> _cachedInstances;

        public FallbackBuiltinModule(PythonLanguageVersion version)
            : base(BuiltinTypeId.Unknown.GetModuleName(version)) {
            LanguageVersion = version;
            _cachedInstances = new Dictionary<BuiltinTypeId, IMember>();
        }

        private IMember GetOrCreate(BuiltinTypeId typeId) {
            if (typeId.IsVirtualId()) {
                switch (typeId) {
                    case BuiltinTypeId.Str:
                        typeId = LanguageVersion.Is3x() ? BuiltinTypeId.Unicode : BuiltinTypeId.Bytes;
                        break;
                    case BuiltinTypeId.StrIterator:
                        typeId = LanguageVersion.Is3x() ? BuiltinTypeId.UnicodeIterator : BuiltinTypeId.BytesIterator;
                        break;
                    default:
                        typeId = BuiltinTypeId.Unknown;
                        break;
                }
            }

            lock (_cachedInstances) {
                if (!_cachedInstances.TryGetValue(typeId, out var value)) {
                    _cachedInstances[typeId] = value = new FallbackBuiltinPythonType(this, typeId);
                }
                return value;
            }
        }

        public IMember GetAnyMember(string name) {
            foreach (BuiltinTypeId typeId in Enum.GetValues(typeof(BuiltinTypeId))) {
                if (typeId.GetTypeName(LanguageVersion) == name) {
                    return GetOrCreate(typeId);
                }
            }
            return GetOrCreate(BuiltinTypeId.Unknown);
        }

        public IEnumerable<string> GetChildrenModules() => Enumerable.Empty<string>();
        public void Imported(IModuleContext context) { }
    }

    class FallbackBuiltinPythonType : AstPythonType {
        public FallbackBuiltinPythonType(FallbackBuiltinModule declaringModule, BuiltinTypeId typeId) :
            base(typeId.GetModuleName(declaringModule.LanguageVersion), declaringModule, declaringModule.Documentation, null) {
            TypeId = typeId;
        }

        public override PythonMemberType MemberType => PythonMemberType.Class;
        public override BuiltinTypeId TypeId { get; }
    }
}
