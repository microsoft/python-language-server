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
using Microsoft.Python.Analysis.Specializations.Typing;
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
        public NamedTupleModel[] NamedTupleBases { get; set; }
        public FunctionModel[] Methods { get; set; }
        public PropertyModel[] Properties { get; set; }
        public VariableModel[] Fields { get; set; }
        public ClassModel[] Classes { get; set; }

        /// <summary>
        /// GenericParameters of the Generic[...] base class, if any.
        /// </summary>
        public string[] GenericBaseParameters { get; set; }
        /// <summary>
        /// Values assigned to the generic parameters, if any.
        /// </summary>
        public GenericParameterValueModel[] GenericParameterValues { get; set; }

        [NonSerialized] private readonly ReentrancyGuard<IMember> _processing = new ReentrancyGuard<IMember>();
        [NonSerialized] private PythonClassType _cls;

        public ClassModel() { } // For de-serializer from JSON

        public ClassModel(IPythonClassType cls) {
            var methods = new List<FunctionModel>();
            var properties = new List<PropertyModel>();
            var fields = new List<VariableModel>();
            var innerClasses = new List<ClassModel>();

            // Skip certain members in order to avoid infinite recursion.
            foreach (var name in cls.GetMemberNames().Except(new[] { "__base__", "__bases__", "__class__", "mro" })) {
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

            // Only persist documentation from this class, leave bases or __init__ alone.
            Documentation = (cls as PythonClassType)?.DocumentationSource == PythonClassType.ClassDocumentationSource.Class ? cls.Documentation : null;

            var ntBases = cls.Bases.OfType<ITypingNamedTupleType>().ToArray();
            NamedTupleBases = ntBases.Select(b => new NamedTupleModel(b)).ToArray();

            Bases = cls.Bases.Except(ntBases).Select(t => t.GetPersistentQualifiedName()).ToArray();
            Methods = methods.ToArray();
            Properties = properties.ToArray();
            Fields = fields.ToArray();
            Classes = innerClasses.ToArray();

            if (cls.IsGeneric) {
                // Only check immediate bases, i.e. when class itself has Generic[T] base.
                var gcp = cls.Bases.OfType<IGenericClassBase>().FirstOrDefault();
                GenericBaseParameters = gcp?.TypeParameters.Select(p => p.Name).ToArray();
            }
            // If class is generic, we must save its generic base definition
            // so on restore we'll be able to re-create the class as generic.
            GenericBaseParameters = GenericBaseParameters ?? Array.Empty<string>();

            GenericParameterValues = cls.GenericParameters
                .Select(p => new GenericParameterValueModel { Name = p.Key, Type = p.Value.GetPersistentQualifiedName() })
                .ToArray();
        }

        public override IMember Create(ModuleFactory mf, IPythonType declaringType, IGlobalScope gs) {
            if(_cls != null) {
                return _cls;
            }
            _cls = new PythonClassType(Name, new Location(mf.Module, IndexSpan.ToSpan()));
            var bases = CreateBases(mf, gs);

            _cls.SetBases(bases);
            _cls.SetDocumentation(Documentation);

            if (GenericParameterValues.Length > 0) {
                _cls.StoreGenericParameters(
                    _cls,
                    _cls.GenericParameters.Keys.ToArray(),
                    GenericParameterValues.ToDictionary(
                        k => _cls.GenericParameters.Keys.First(x => x == k.Name),
                        v => mf.ConstructType(v.Type)
                    )
                );
            }

            var all = Classes.Concat<MemberModel>(Properties).Concat(Methods).Concat(Fields);
            foreach (var m in all) {
                _cls.AddMember(m.Name, m.Create(mf, _cls, gs), false);
            }
            return _cls;
        }

        public override void Populate(ModuleFactory mf, IPythonType declaringType, IGlobalScope gs) {
            var all = Classes.Concat<MemberModel>(Properties).Concat(Methods).Concat(Fields);
            foreach (var m in all) {
                m.Populate(mf, _cls, gs);
            }
        }

        private IEnumerable<IPythonType> CreateBases(ModuleFactory mf, IGlobalScope gs) {
            var ntBases = NamedTupleBases.Select(ntb => {
                var n = ntb.Create(mf, _cls, gs);
                ntb.Populate(mf, _cls, gs);
                return n;
            }).OfType<IPythonType>().ToArray();

            var is3x = mf.Module.Interpreter.LanguageVersion.Is3x();
            var basesNames = Bases.Select(b => is3x && b == "object" ? null : b).ExcludeDefault().ToArray();
            var bases = basesNames.Select(mf.ConstructType).ExcludeDefault().Concat(ntBases).ToArray();

            if (GenericBaseParameters.Length > 0) {
                // Generic class. Need to reconstruct generic base so code can then
                // create specific types off the generic class.
                var genericBase = bases.OfType<IGenericType>().FirstOrDefault(b => b.Name == "Generic");
                if (genericBase != null) {
                    var typeVars = GenericBaseParameters.Select(n => gs.Variables[n]?.Value).OfType<IGenericTypeParameter>().ToArray();
                    //Debug.Assert(typeVars.Length > 0, "Class generic type parameters were not defined in the module during restore");
                    if (typeVars.Length > 0) {
                        var genericWithParameters = genericBase.CreateSpecificType(new ArgumentSet(typeVars, null, null));
                        if (genericWithParameters != null) {
                            bases = bases.Except(Enumerable.Repeat(genericBase, 1)).Concat(Enumerable.Repeat(genericWithParameters, 1)).ToArray();
                        }
                    }
                } else {
                    Debug.Fail("Generic class does not have generic base.");
                }
            }
            return bases;
        }

        protected override IEnumerable<MemberModel> GetMemberModels()
            => Classes.Concat<MemberModel>(Methods).Concat(Properties).Concat(Fields);
    }
}
