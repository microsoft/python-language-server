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
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Types {
    internal sealed class ParameterInfo : IParameterInfo {
        public ParameterInfo(PythonAst ast, Parameter p, IPythonType type, bool isGeneric) {
            Name = p?.Name ?? throw new ArgumentNullException(nameof(p));
            Documentation = string.Empty;
            DefaultValueString = p.DefaultValue?.ToCodeString(ast).Trim();
            if (DefaultValueString == "...") {
                DefaultValueString = null;
            }
            IsParamArray = p.Kind == ParameterKind.List;
            IsKeywordDict = p.Kind == ParameterKind.Dictionary;
            IsGeneric = isGeneric;
            Type = type;
        }

        public string Name { get; }
        public string Documentation { get; }
        public bool IsParamArray { get; }
        public bool IsKeywordDict { get; }
        public bool IsGeneric { get; }
        public IPythonType Type { get; private set; }
        public string DefaultValueString { get; }
        public IPythonType DefaultValueType { get; private set; }

        internal void SetType(IPythonType type) {
            if (Type.IsUnknown()) {
                Type = type;
            }
        }
        internal void SetDefaultValueType(IPythonType type) {
            if (DefaultValueType.IsUnknown()) {
                DefaultValueType = type;
                SetType(type);
            }
        }
    }
}
