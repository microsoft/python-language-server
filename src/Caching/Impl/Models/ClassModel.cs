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
using System.Diagnostics;
using System.Linq;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Utilities;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
// ReSharper disable MemberCanBePrivate.Global

namespace Microsoft.Python.Analysis.Caching.Models {
    [Serializable]
    [DebuggerDisplay("cls:{" + nameof(Name) + "}")]
    internal sealed class ClassModel : MemberModel {
        public string Documentation { get; set; }
        public string[] Bases { get; set; }
        public FunctionModel[] Methods { get; set; }
        public PropertyModel[] Properties { get; set; }
        public VariableModel[] Fields { get; set; }
        public string[] GenericParameters { get; set; }
        public ClassModel[] Classes { get; set; }

        [NonSerialized]
        private readonly ReentrancyGuard<IMember> _processing = new ReentrancyGuard<IMember>();

        public ClassModel() { } // For de-serializer from JSON

        public ClassModel(IPythonClassType cls) {
            var methods = new List<FunctionModel>();
            var properties = new List<PropertyModel>();
            var fields = new List<VariableModel>();
            var innerClasses = new List<ClassModel>();

            // Skip certain members in order to avoid infinite recursion.
            foreach (var name in cls.GetMemberNames().Except(new[] {"__base__", "__bases__", "__class__", "mro"})) {
                var m = cls.GetMember(name);

                // Only take members from this class, skip members from bases.
                if (m is IPythonClassMember cm && cls.QualifiedName != cm.DeclaringType?.QualifiedName) {
                    continue;
                }

                using (_processing.Push(m, out var reentered)) {
                    if (reentered) {
                        continue;
                    }

                    switch (m) {
                        case IPythonClassType ct when ct.Name == name:
                            if (!ct.DeclaringModule.Equals(cls.DeclaringModule)) {
                                continue;
                            }
                            innerClasses.Add(new ClassModel(ct));
                            break;
                        case IPythonFunctionType ft when ft.IsLambda():
                            break;
                        case IPythonFunctionType ft when ft.Name == name:
                            methods.Add(new FunctionModel(ft));
                            break;
                        case IPythonPropertyType prop when prop.Name == name:
                            properties.Add(new PropertyModel(prop));
                            break;
                        case IPythonInstance inst:
                            fields.Add(VariableModel.FromInstance(name, inst));
                            break;
                        case IPythonType t:
                            fields.Add(VariableModel.FromType(name, t));
                            break;
                    }
                }
            }

            Name = cls.TypeId == BuiltinTypeId.Ellipsis ? "ellipsis" : cls.Name;
            Id = Name.GetStableHash();
            QualifiedName = cls.QualifiedName;
            IndexSpan = cls.Location.IndexSpan.ToModel();

            Documentation = cls.Documentation;
            Bases = cls.Bases.OfType<IPythonClassType>().Select(t => t.GetPersistentQualifiedName()).ToArray();
            Methods = methods.ToArray();
            Properties = properties.ToArray();
            Fields = fields.ToArray();
            Classes = innerClasses.ToArray();
        }

        protected override IMember DoConstruct(ModuleFactory mf, IPythonType declaringType) {
            var cls = new PythonClassType(Name, new Location(mf.Module, IndexSpan.ToSpan()));
            // In Python 3 exclude object since type creation will add it automatically.
            var is3x = mf.Module.Interpreter.LanguageVersion.Is3x();
            var bases = Bases.Select(b => is3x && b == "object" ? null : mf.ConstructType(b)).ExcludeDefault().ToArray();
            cls.SetBases(bases);
            cls.SetDocumentation(Documentation);

            foreach (var f in Methods) {
                var m = f.Construct(mf, cls);
                cls.AddMember(f.Name, m, false);
            }

            foreach (var p in Properties) {
                var m = p.Construct(mf, cls);
                cls.AddMember(p.Name, m, false);
            }

            foreach (var c in Classes) {
                var m = c.Construct(mf, cls);
                cls.AddMember(c.Name, m, false);
            }

            foreach (var vm in Fields) {
                var m = vm.Construct(mf, cls);
                cls.AddMember(vm.Name, m, false);
            }

            return cls;
        }

        protected override IEnumerable<MemberModel> GetMemberModels() 
            => Classes.Concat<MemberModel>(Methods).Concat(Properties).Concat(Fields);
    }
}
