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
using Microsoft.Python.Analysis.Specializations.Typing;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer.Evaluation {
    internal sealed class TypeAnnotationConverter : TypeAnnotationConverter<IPythonType> {
        private readonly ExpressionEval _eval;
        private readonly LookupOptions _options;

        public TypeAnnotationConverter(ExpressionEval eval, 
            LookupOptions options = LookupOptions.Global | LookupOptions.Builtins) {
            _eval = eval ?? throw new ArgumentNullException(nameof(eval));
            _options = options;
        }

        public override IPythonType Finalize(IPythonType type) {
            if (type.IsUnknown() || type is IPythonModule) {
                return null;
            }

            var n = GetName(type);
            if (!string.IsNullOrEmpty(n)) {
                return _eval.LookupNameInScopes(n).GetPythonType();
            }

            return type;
        }

        public override IPythonType LookupName(string name)
            => _eval.LookupNameInScopes(name, _options)?.GetPythonType();

        public override IPythonType GetTypeMember(IPythonType baseType, string member) 
            => baseType.GetMember(member)?.GetPythonType();

        public override IPythonType MakeGeneric(IPythonType baseType, IReadOnlyList<IPythonType> args) {
            if (baseType is IGenericType gt) {
                return gt.CreateSpecificType(args, _eval.Module, LocationInfo.Empty);
            }
            if(baseType is IPythonClassType cls && cls.IsGeneric()) {
                // Type is not yet known for generic classes. Resolution is delayed
                // until specific type is instantiated.
                return cls;
            }
            // TODO: report unhandled generic?
            return null;
        }
    }
}
