// Python Tools for Visual Studio
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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonParameterInfo : IParameterInfo {
        private readonly IPythonType[] _lazyParameterTypes;

        public AstPythonParameterInfo(PythonAst ast, Parameter p, IEnumerable<IPythonType> types) {
            Name = p?.Name ?? throw new ArgumentNullException(nameof(p));
            Documentation = "";
            DefaultValue = p.DefaultValue?.ToCodeString(ast).Trim();
            if (DefaultValue == "...") {
                DefaultValue = null;
            }
            IsParamArray = p.Kind == ParameterKind.List;
            IsKeywordDict = p.Kind == ParameterKind.Dictionary;
            _lazyParameterTypes = types.MaybeEnumerate().ToArray();
        }

        public string Name { get; }
        public string Documentation { get; }
        public string DefaultValue { get; }
        public bool IsParamArray { get; }
        public bool IsKeywordDict { get; }
        public IList<IPythonType> ParameterTypes
            => _lazyParameterTypes.Select(p => p.ResolveType()).OfType<IPythonType>().ToList();
    }
}
