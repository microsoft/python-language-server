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
// ReSharper disable MemberCanBeProtected.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Microsoft.Python.Analysis.Caching.Models {
    [Serializable]
    internal abstract class MemberModel {
        /// <summary>
        /// Member unique id in the database.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Member name, such as name of a class.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Member qualified name within the module, such as A.B.C.
        /// </summary>
        public string QualifiedName { get; set; }

        /// <summary>
        /// Member location in the module original source code.
        /// </summary>
        public IndexSpanModel IndexSpan { get; set; }

        [NonSerialized]
        private IMember _member;

        public IMember Construct(ModuleFactory mf, IPythonType declaringType) 
            => _member ?? (_member = ReConstruct(mf, declaringType));
        protected abstract IMember ReConstruct(ModuleFactory mf, IPythonType declaringType);

        public MemberModel GetModel(string name) => GetMemberModels().FirstOrDefault(m => m.Name == name);
        protected virtual IEnumerable<MemberModel> GetMemberModels() => Enumerable.Empty<MemberModel>();
    }
}
