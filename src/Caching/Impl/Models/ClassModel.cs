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
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;

namespace Microsoft.Python.Analysis.Caching.Models {
    internal sealed class ClassModel {
        public string Name { get; set; }
        public string[] Bases { get; set; }
        public FunctionModel[] Methods { get; set; }
        public PropertyModel[] Properties { get; set; }
        public VariableModel[] Fields { get; set; }
        public string[] GenericParameters { get; set; }
        public ClassModel[] InnerClasses { get; set; }

        public static ClassModel FromType(IPythonClassType cls) {
            var methods = new List<FunctionModel>();
            var properties = new List<PropertyModel>();
            var fields = new List<VariableModel>();
            var innerClasses = new List<ClassModel>();

            foreach (var name in cls.GetMemberNames()) {
                var m = cls.GetMember(name);
                switch (m) {
                    case IPythonClassType ct:
                        innerClasses.Add(FromType(ct));
                        break;
                    case IPythonFunctionType ft:
                        methods.Add(FunctionModel.FromType(ft));
                        break;
                    case IPythonPropertyType prop:
                        properties.Add(PropertyModel.FromType(prop));
                        break;
                    case IPythonInstance inst:
                        fields.Add(VariableModel.FromInstance(name, inst));
                        break;
                    case IPythonType t:
                        fields.Add(VariableModel.FromType(name, t));
                        break;
                }
            }

            return new ClassModel {
                Name = cls.Name,
                Bases = cls.Bases.OfType<IPythonClassType>().Select(t => t.GetQualifiedName()).ToArray(),
                Methods = methods.ToArray(),
                Properties = properties.ToArray(),
                Fields = fields.ToArray(),
                InnerClasses = innerClasses.ToArray()
            };
        }
    }
}
