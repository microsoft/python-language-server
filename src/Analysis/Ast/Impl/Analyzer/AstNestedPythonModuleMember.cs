﻿// Copyright(c) Microsoft Corporation
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
using System.Threading;

namespace Microsoft.Python.Analysis.Analyzer {
    internal sealed class AstNestedPythonModuleMember : ILazyMember {
        private volatile IMember _realMember;

        public AstNestedPythonModuleMember(
            string memberName,
            AstNestedPythonModule module,
            LocationInfo importLocation
        ) {
            Name = memberName ?? throw new ArgumentNullException(nameof(memberName));
            Module = module ?? throw new ArgumentNullException(nameof(module));
            ImportLocation = importLocation;
        }

        public string Name { get; }
        public AstNestedPythonModule Module { get; }
        public LocationInfo ImportLocation { get; }

        public PythonMemberType MemberType => PythonMemberType.Unknown;

        public IMember Get() {
            var m = _realMember;
            if (m != null) {
                return m;
            }

            var interp = _context as AstPythonInterpreter;
            if (interp == null) {
                return null;
            }

            // Set an "unknown" value to prevent recursion
            var locs = ImportLocation == null ? Array.Empty<LocationInfo>() : new[] { ImportLocation };
            var sentinel = new AstPythonConstant(interp.GetBuiltinType(BuiltinTypeId.Unknown), locs);
            m = Interlocked.CompareExchange(ref _realMember, sentinel, null);
            if (m != null) {
                // We raced and someone else set a value, so just return that
                return m;
            }

            Module.NotifyImported();
            m = Module.GetMember(Name) ?? interp.ImportModule(Module.Name + "." + Name);
            if (m != null) {
                (m as IPythonModule)?.NotifyImported();
                var current = Interlocked.CompareExchange(ref _realMember, m, sentinel);
                if (current == sentinel) {
                    return m;
                }
                return current;
            }

            // Did not find a better member, so keep the sentinel
            return sentinel;
        }
    }
}
