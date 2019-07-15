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

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Utilities;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;

namespace Microsoft.Python.Analysis.Caching.Models {
    [DebuggerDisplay("c:{Name}")]
    internal sealed class ClassModel: MemberModel {
        public string Documentation { get; set; }
        public string[] Bases { get; set; }
        public FunctionModel[] Methods { get; set; }
        public PropertyModel[] Properties { get; set; }
        public VariableModel[] Fields { get; set; }
        public string[] GenericParameters { get; set; }
        public ClassModel[] InnerClasses { get; set; }

        private readonly ReentrancyGuard<IMember> _processing = new ReentrancyGuard<IMember>();

        public static ClassModel FromType(IPythonClassType cls) => new ClassModel(cls);

        public ClassModel() { } // For de-serializer from JSON

        private ClassModel(IPythonClassType cls) {
            var methods = new List<FunctionModel>();
            var properties = new List<PropertyModel>();
            var fields = new List<VariableModel>();
            var innerClasses = new List<ClassModel>();

            // Skip certain members in order to avoid infinite recursion.
            foreach (var name in cls.GetMemberNames().Except(new[] { "__base__", "__bases__", "__class__", "mro" })) {
                var m = cls.GetMember(name);

                // Only take members from this class, skip members from bases.
                if (m is IPythonClassMember cm && !cls.Equals(cm.DeclaringType)) {
                    continue;
                }

                if (!_processing.Push(m)) {
                    continue;
                }

                try {
                    switch (m) {
                        case IPythonClassType ct when ct.Name == name:
                            if (!ct.DeclaringModule.Equals(cls.DeclaringModule)) {
                                continue;
                            }
                            innerClasses.Add(FromType(ct));
                            break;
                        case IPythonFunctionType ft when ft.Name == name:
                            methods.Add(FunctionModel.FromType(ft));
                            break;
                        case IPythonPropertyType prop when prop.Name == name:
                            properties.Add(PropertyModel.FromType(prop));
                            break;
                        case IPythonInstance inst:
                            fields.Add(VariableModel.FromInstance(name, inst));
                            break;
                        case IPythonType t:
                            fields.Add(VariableModel.FromType(name, t));
                            break;
                    }
                } finally {
                    _processing.Pop();
                }
            }

            Name = cls.TypeId == BuiltinTypeId.Ellipsis ? "ellipsis" : cls.Name;
            Id = Name.GetStableHash();
            IndexSpan = cls.Location.IndexSpan.ToModel();

            Documentation = cls.Documentation;
            Bases = cls.Bases.OfType<IPythonClassType>().Select(t => t.GetQualifiedName()).ToArray();
            Methods = methods.ToArray();
            Properties = properties.ToArray();
            Fields = fields.ToArray();
            InnerClasses = innerClasses.ToArray();
        }
    }
}
