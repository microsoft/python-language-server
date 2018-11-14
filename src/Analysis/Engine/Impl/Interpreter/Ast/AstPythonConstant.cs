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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonConstant : IPythonConstant, IMemberContainer, ILocatedMember {
        private readonly Dictionary<string, IMember> _cachedMembers = new Dictionary<string, IMember>();

        public AstPythonConstant(IPythonType type, params ILocationInfo[] locations) {
            Type = type ?? throw new ArgumentNullException(nameof(type));
            Locations = locations.ToArray();
        }

        public IEnumerable<ILocationInfo> Locations { get; }
        public PythonMemberType MemberType => PythonMemberType.Constant;
        public IPythonType Type { get; }

        public IMember GetMember(IModuleContext context, string name) {
            lock (_cachedMembers) {
                if (!_cachedMembers.TryGetValue(name, out var m)) {
                    m = Type?.GetMember(context, name);
                    _cachedMembers[name] = m;
                }
                return m;
            }
        }

        public IEnumerable<string> GetMemberNames(IModuleContext moduleContext)
            => Type?.GetMemberNames(moduleContext);
    }
}
