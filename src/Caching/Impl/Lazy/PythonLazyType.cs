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
    internal abstract class PythonLazyType<TModel> : PythonTypeWrapper where TModel : class {
        protected IGlobalScope _gs;
        protected ModuleFactory _mf;
        protected TModel _model;

        protected PythonLazyType(TModel model, ModuleFactory mf, IGlobalScope gs, IPythonType declaringType) {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _mf = mf ?? throw new ArgumentNullException(nameof(mf));
            _gs = gs ?? throw new ArgumentNullException(nameof(gs));
        }

        protected abstract void EnsureContent();
        protected void ReleaseModel() {
            _mf = null;
            _gs = null;
            _model = null;
        }
    }
}
