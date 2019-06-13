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

using System.Collections.Generic;
using System.Linq;
using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Analysis.Types;

namespace Microsoft.Python.Analysis.Caching.Factories {
    internal sealed class ClassFactory : FactoryBase<ClassModel, IPythonClassType> {
        public ClassFactory(IEnumerable<ClassModel> classes, ModuleFactory mf)
            : base(classes, mf) {
        }

        protected override IPythonClassType CreateMember(ClassModel cm, IPythonType declaringType) {

            var cls = new PythonClassType(cm.Name, new Location(ModuleFactory.Module));
            cls.SetBases(cm.Bases.Select(b => Construct(b, null)));

            foreach (var f in cm.Methods) {
                cls.AddMember(f.Name, ModuleFactory.FunctionFactory.Construct(f, cls), false);
            }
            foreach (var p in cm.Properties) {
                cls.AddMember(p.Name, ModuleFactory.PropertyFactory.Construct(p, cls), false);
            }
            foreach (var c in cm.InnerClasses) {
                cls.AddMember(c.Name, Construct(c, cls), false);
            }
            // TODO: fields. Bypass variable cache!
            return cls;
        }
    }
}
