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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Caching.Lazy {
    /// <summary>
    /// Represents 'lazy' type that delays creation of its content such as members,
    /// function parameters and return types until they are requested. This allows
    /// deferred fetching of data from the database, avoiding wholesale restore.
    /// </summary>
    internal abstract class PythonLazyType<TModel> : PythonTypeWrapper where TModel : class {
        protected IGlobalScope GlobalScope { get; private set; }
        protected ModuleFactory ModuleFactory { get; private set; }
        protected TModel Model { get; private set; }

        protected PythonLazyType(TModel model, ModuleFactory mf, IGlobalScope gs, IPythonType declaringType) {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            ModuleFactory = mf ?? throw new ArgumentNullException(nameof(mf));
            GlobalScope = gs ?? throw new ArgumentNullException(nameof(gs));
            DeclaringType = declaringType;
        }

        public IPythonType DeclaringType { get; }

        protected abstract void EnsureContent();
        protected void ReleaseModel() {
            ModuleFactory = null;
            GlobalScope = null;
            Model = null;
        }
    }
}
