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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
// ReSharper disable MemberCanBeProtected.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Microsoft.Python.Analysis.Caching.Models {
    [Serializable]
    internal abstract class MemberModel {
        [NonSerialized] protected ModuleFactory _mf;
        [NonSerialized] protected IGlobalScope _gs;
        [NonSerialized] private bool _finalizing;

        /// <summary>
        /// Member unique id in the database.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Member name, such as name of a class.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Unique id of declaring module.
        /// </summary>
        public string DeclaringModuleId { get; set; }

        /// <summary>
        /// Member qualified name within the module, such as A.B.C.
        /// </summary>
        public string QualifiedName { get; set; }

        /// <summary>
        /// Member location in the module original source code.
        /// </summary>
        public IndexSpanModel IndexSpan { get; set; }

        /// <summary>
        /// Creates member for declaration but does not construct its parts just yet.
        /// Used as a first pass in two-pass handling of forward declarations.
        /// </summary>
        public IMember Declare(ModuleFactory mf, IPythonType declaringType, IGlobalScope gs) {
            _mf = mf;
            _gs = gs;
            return DeclareMember(declaringType);
        }

        /// <summary>
        /// Populates member with content, such as create class methods, etc.
        /// </summary>
        public void Finalize() {
            if (!_finalizing) {
                _finalizing = true;
                FinalizeMember();
                _finalizing = false;
            }
        }

        protected abstract IMember DeclareMember(IPythonType declaringType);
        protected abstract void FinalizeMember();

        public virtual MemberModel GetModel(string name) => GetMemberModels().FirstOrDefault(m => m.Name == name);
        protected virtual IEnumerable<MemberModel> GetMemberModels() => Enumerable.Empty<MemberModel>();
    }
}
