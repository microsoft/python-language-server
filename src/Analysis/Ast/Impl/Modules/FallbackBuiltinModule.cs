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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing;

namespace Microsoft.Python.Analysis.Modules {
    internal sealed class FallbackBuiltinsModule : PythonModule, IBuiltinsPythonModule {
        public readonly PythonLanguageVersion LanguageVersion;
        private readonly Dictionary<BuiltinTypeId, IPythonType> _cachedInstances;

        public FallbackBuiltinsModule(PythonLanguageVersion version)
            : base(BuiltinTypeId.Unknown.GetModuleName(version), ModuleType.Builtins, null) {
            LanguageVersion = version;
            _cachedInstances = new Dictionary<BuiltinTypeId, IPythonType>();
        }

        private IPythonType GetOrCreate(BuiltinTypeId typeId) {
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
    }

    class FallbackBuiltinPythonType : PythonType {
        public FallbackBuiltinPythonType(FallbackBuiltinsModule declaringModule, BuiltinTypeId typeId) :
            base(typeId.GetModuleName(declaringModule.LanguageVersion), declaringModule, declaringModule.Documentation, null) {
            TypeId = typeId;
        }

        public override PythonMemberType MemberType => PythonMemberType.Class;
        public override BuiltinTypeId TypeId { get; }
    }
}
