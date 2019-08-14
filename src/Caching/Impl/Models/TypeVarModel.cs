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

using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Specializations.Typing.Types;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Analysis.Values;
using Microsoft.Python.Core;
// ReSharper disable MemberCanBePrivate.Global

namespace Microsoft.Python.Analysis.Caching.Models {
    [Serializable]
    [DebuggerDisplay("TypeVar:{" + nameof(Name) + "}")]
    internal sealed class TypeVarModel : MemberModel {
        public string[] Constraints { get; set; }
        public string Bound { get; set; }
        public string Covariant { get; set; }
        public string Contravariant { get; set; }

        public static TypeVarModel FromGeneric(IVariable v) {
            var g = (IGenericTypeParameter)v.Value;
            return new TypeVarModel {
                Id = g.Name.GetStableHash(),
                Name = g.Name,
                QualifiedName = g.QualifiedName,
                Constraints = g.Constraints.Select(c => c.GetPersistentQualifiedName()).ToArray(),
                Bound = g.Bound.GetPersistentQualifiedName(),
                Covariant = g.Covariant.GetPersistentQualifiedName(),
                Contravariant = g.Contravariant.GetPersistentQualifiedName()
            };
        }

        protected override IMember DoConstruct(ModuleFactory mf, IPythonType declaringType) {
            var constraints = Constraints.Select(mf.ConstructType).ToArray();
            var bound = mf.ConstructType(Bound);
            var covariant = mf.ConstructType(Covariant);
            var contravariant = mf.ConstructType(Contravariant);
            return new GenericTypeParameter(Name, mf.Module, constraints, bound, covariant, contravariant, mf.DefaultLocation);
        }
    }
}
