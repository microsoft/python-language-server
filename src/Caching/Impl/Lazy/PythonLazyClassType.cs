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
using Microsoft.Python.Analysis.Caching.Models;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Caching.Lazy {
    internal sealed class PythonLazyClassType : PythonLazyType<ClassModel>, IPythonClassType {
        private readonly PythonClassType _cls;

        public PythonLazyClassType(ClassModel model, ModuleFactory mf, IPythonType declaringType)
            : base(model, mf, null, declaringType) {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _cls = new PythonClassType(Name, new Location(mf.Module, model.IndexSpan.ToSpan()));
            _cls.SetDocumentation(Documentation);
            SetInnerType(_cls);
        }

        #region IPythonType
        public override IMember GetMember(string name) {
            EnsureContent();
            return base.GetMember(name);
        }

        public override IEnumerable<string> GetMemberNames() {
            EnsureContent();
            return base.GetMemberNames();
        }
        #endregion

        #region IPythonClassType
        public IPythonType CreateSpecificType(IArgumentSet typeArguments) {
            EnsureContent();
            return _cls.CreateSpecificType(typeArguments);
        }

        public IPythonType DeclaringType => _cls.DeclaringType;
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

        protected override void EnsureContent() {
            if (_model == null) {
                return;
            }

            var bases = CreateBases(_mf, _gs);
            _cls.SetBases(bases);

            if (_model.GenericParameterValues.Length > 0) {
                _cls.StoreGenericParameters(
                    _cls,
                    _cls.GenericParameters.Keys.ToArray(),
                    _model.GenericParameterValues.ToDictionary(
                        k => _cls.GenericParameters.Keys.First(x => x == k.Name),
                        v => _mf.ConstructType(v.Type)
                    )
                );
            }

            var all = _model.Classes.Concat<MemberModel>(_model.Properties).Concat(_model.Methods).Concat(_model.Fields).ToArray();
            foreach (var m in all) {
                _cls.AddMember(m.Name, m.CreateDeclaration(_mf, _cls, _gs), false);
            }
            foreach (var m in all) {
                m.CreateContent();
            }
            
            ReleaseModel();
        }

        private IEnumerable<IPythonType> CreateBases(ModuleFactory mf, IGlobalScope gs) {
            var ntBases = _model.NamedTupleBases.Select(ntb => {
                var n = ntb.CreateDeclaration(mf, _cls, gs);
                ntb.CreateContent();
                return n;
            }).OfType<IPythonType>().ToArray();

            var is3x = mf.Module.Interpreter.LanguageVersion.Is3x();
            var basesNames = Bases.Select(b => is3x && b == "object" ? null : b).ExcludeDefault().ToArray();
            var bases = basesNames.Select(mf.ConstructType).ExcludeDefault().Concat(ntBases).ToArray();

            if (_model.GenericBaseParameters.Length > 0) {
                // Generic class. Need to reconstruct generic base so code can then
                // create specific types off the generic class.
                var genericBase = bases.OfType<IGenericType>().FirstOrDefault(b => b.Name == "Generic");
                if (genericBase != null) {
                    var typeVars = _model.GenericBaseParameters.Select(n => gs.Variables[n]?.Value).OfType<IGenericTypeParameter>().ToArray();
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

    }
}
