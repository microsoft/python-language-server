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
using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis.Caching.Factories {
    internal abstract class FactoryBase<TModel, TMember> : IDisposable
        where TModel : MemberModel
        where TMember : IMember {

        private class ModelData {
            public TModel Model;
            public TMember Member;
        }

        private readonly Dictionary<string, ModelData> _data;

        protected ModuleFactory ModuleFactory { get; }

        protected FactoryBase(IEnumerable<TModel> models, ModuleFactory mf) {
            ModuleFactory = mf;
            _data = models.ToDictionary(k => k.Name, v => new ModelData { Model = v });
        }

        public TMember Construct(TModel cm, IPythonType declaringType, bool cached = true) {
            if (!cached) {
                return CreateMember(cm, declaringType);
            }

            var data = _data[cm.Name];
            if (data.Member == null) {
                data.Member = CreateMember(data.Model, declaringType);
            }
            return data.Member;
        }

        public virtual void Dispose() => _data.Clear();

        protected abstract TMember CreateMember(TModel model, IPythonType declaringType);
    }
}
