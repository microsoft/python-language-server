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
using System.Diagnostics;
using System.Linq;
using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Types.Collections;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Caching.Lazy {
    internal sealed class PythonLazyClassType : PythonLazyType<ClassModel>, IPythonClassType {
        private readonly PythonClassType _cls;

        public PythonLazyClassType(ClassModel model, ModuleFactory mf, IGlobalScope gs, IPythonType declaringType)
            : base(model, mf, gs, declaringType) {
            _cls = new PythonClassType(model.Name, new Location(mf.Module, model.IndexSpan.ToSpan()));
            _cls.SetDocumentation(model.Documentation);
            SetInnerType(_cls);
        }

        #region IPythonClassType
        public IPythonType CreateSpecificType(IArgumentSet typeArguments) {
            EnsureContent();
            return _cls.CreateSpecificType(typeArguments);
        }

        public IReadOnlyList<IGenericTypeParameter> Parameters {
            get {
                EnsureContent();
                return _cls.Parameters;
            }
        }

        public bool IsGeneric {
            get {
                EnsureContent();
                return _cls.IsGeneric;
            }
        }

        public ClassDefinition ClassDefinition => null;

        public IReadOnlyList<IPythonType> Mro {
            get {
                EnsureContent();
                return _cls.Mro;
            }
        }

        public IReadOnlyList<IPythonType> Bases {
            get {
                EnsureContent();
                return _cls.Bases;
            }
        }

        public IReadOnlyDictionary<string, IPythonType> GenericParameters {
            get {
                EnsureContent();
                return _cls.GenericParameters;
            }
        }
        #endregion

        protected override void EnsureContent(ClassModel cm) {
            var bases = CreateBases(cm, ModuleFactory, GlobalScope);
            _cls.SetBases(bases);

            if (cm.GenericParameterValues.Length > 0) {
                _cls.StoreGenericParameters(
                    _cls,
                    _cls.GenericParameters.Keys.ToArray(),
                    cm.GenericParameterValues.ToDictionary(
                        k => _cls.GenericParameters.Keys.First(x => x == k.Name),
                        v => ModuleFactory.ConstructType(v.Type)
                    )
                );
            }

            var allMemberModels = cm.Classes
                .Concat<MemberModel>(cm.Properties)
                .Concat(cm.Methods)
                .Concat(cm.Fields)
                .ToArray();

            foreach (var model in allMemberModels) {
                _cls.AddMember(model.Name, MemberFactory.CreateMember(model, ModuleFactory, GlobalScope, _cls), false);
            }
            _cls.AddMember("__class__", _cls, true);
        }

        private IEnumerable<IPythonType> CreateBases(ClassModel cm, ModuleFactory mf, IGlobalScope gs) {
            var ntBases = cm.NamedTupleBases
                .Select(ntb => MemberFactory.CreateMember(ntb, ModuleFactory, GlobalScope, _cls))
                .OfType<IPythonType>()
                .ToArray();

            var is3x = mf.Module.Interpreter.LanguageVersion.Is3x();
            var basesNames = cm.Bases.Select(b => is3x && b == "object" ? null : b).ExcludeDefault().ToArray();
            var bases = basesNames.Select(mf.ConstructType).ExcludeDefault().Concat(ntBases).ToArray();

            // Make sure base types are realized
            foreach (var b in bases.OfType<PythonLazyClassType>()) {
                b.EnsureContent();
            }

            if (cm.GenericBaseParameters.Length > 0) {
                // Generic class. Need to reconstruct generic base so code can then
                // create specific types off the generic class.
                var genericBase = bases.OfType<IGenericType>().FirstOrDefault(b => b.Name == "Generic");
                if (genericBase != null) {
                    var typeVars = cm.GenericBaseParameters.Select(n => gs.Variables[n]?.Value).OfType<IGenericTypeParameter>().ToArray();
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

            if (bases.Length > 0) {
                _cls.AddMember("__base__", bases[0], true);
            }
            _cls.AddMember("__bases__", PythonCollectionType.CreateList(DeclaringModule.Interpreter.ModuleResolution.BuiltinsModule, bases), true);
            return bases;
        }
    }
}
