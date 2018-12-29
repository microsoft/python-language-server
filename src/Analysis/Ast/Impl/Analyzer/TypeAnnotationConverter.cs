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
using Microsoft.Python.Analysis.Analyzer;
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Types {
    internal sealed class TypeAnnotationConverter : TypeAnnotationConverter<IPythonType> {
        private readonly ExpressionLookup _lookup;

        public TypeAnnotationConverter(ExpressionLookup lookup) {
            _lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
        }

        public override IPythonType Finalize(IPythonType type) {
            if (type.IsUnknown() || type is IPythonModule) {
                return null;
            }

            var n = GetName(type);
            if (!string.IsNullOrEmpty(n)) {
                return _lookup.LookupNameInScopes(n).GetPythonType();
            }

            return type;
        }

        private IEnumerable<IPythonType> FinalizeList(IPythonType type) {
            //if (type is UnionType ut) {
            //    foreach (var t in ut.Types.MaybeEnumerate()) {
            //        yield return Finalize(t);
            //    }
            //    yield break;
            //}

            yield return Finalize(type);
        }

        public override IPythonType LookupName(string name)
            => _lookup.LookupNameInScopes(name, ExpressionLookup.LookupOptions.Global | ExpressionLookup.LookupOptions.Builtins)?.GetPythonType();

        public override IPythonType GetTypeMember(IPythonType baseType, string member) 
            => baseType.GetMember(member)?.GetPythonType();

        public override IPythonType MakeGeneric(IPythonType baseType, IReadOnlyList<IPythonType> args) {
            if (!(baseType is IGenericType gt)) {
                // TODO: report unhandled generic?
                return null;
            }
            return gt.CreateSpecificType(args, _lookup.Module, LocationInfo.Empty);
        }
    }
}
