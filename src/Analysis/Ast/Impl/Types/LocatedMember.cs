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
using Microsoft.Python.Analysis.Modules;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Types {
    internal abstract class LocatedMember : ILocatedMember {
        private HashSet<Location> _references;

        protected LocatedMember(PythonMemberType memberType, IPythonModule declaringModule, IndexSpan location = default, ILocatedMember parent = null)
            : this(declaringModule, location, parent) {
            MemberType = memberType;
        }

        protected LocatedMember(IPythonModule declaringModule, IndexSpan location = default, ILocatedMember parent = null) {
            DeclaringModule = declaringModule;
            Parent = parent;
            Parent?.AddReference(declaringModule, location);
            Location = new Location(declaringModule, location);
        }

        public virtual PythonMemberType MemberType { get; }

        public virtual IPythonModule DeclaringModule { get; private set; }

        public virtual LocationInfo Definition => Location.LocationInfo;

        public ILocatedMember Parent { get; }

        public virtual IReadOnlyList<LocationInfo> References {
            get {
                lock (this) {
                    if (_references == null) {
                        return new[] { Definition };
                    }

                    var refs = _references
                        .GroupBy(x => x.LocationInfo.DocumentUri)
                        .SelectMany(g => g.Select(x => x.LocationInfo).OrderBy(x => x.Span));
                    return Enumerable.Repeat(Definition, 1).Concat(refs).ToArray();
                }
            }
        }

        public virtual void AddReference(IPythonModule module, IndexSpan location) {
            lock (this) {
                // Don't add references to library code.
                if (module?.ModuleType == ModuleType.User && (module != DeclaringModule || location != Location.IndexSpan)) {
                    _references = _references ?? new HashSet<Location>();
                    _references.Add(new Location(module, location));
                }
            }
        }

        public virtual void RemoveReferences(IPythonModule module) {
            lock (this) {
                if (_references != null) {
                    foreach (var r in _references.ToArray().Where(r => r.Module == module)) {
                        _references.Remove(r);
                    }
                }
            }
        }

        internal Location Location { get; set; }

        protected void SetDeclaringModule(IPythonModule module) => DeclaringModule = module;
    }

    internal abstract class EmptyLocatedMember : ILocatedMember {
        protected EmptyLocatedMember(PythonMemberType memberType) {
            MemberType = memberType;
        }

        public PythonMemberType MemberType { get; }
        public IPythonModule DeclaringModule => null;
        public LocationInfo Definition => LocationInfo.Empty;
        public ILocatedMember Parent => null;
        public IReadOnlyList<LocationInfo> References => Array.Empty<LocationInfo>();
        public void AddReference(IPythonModule module, IndexSpan location) { }
        public void RemoveReferences(IPythonModule module) { }
    }
}
