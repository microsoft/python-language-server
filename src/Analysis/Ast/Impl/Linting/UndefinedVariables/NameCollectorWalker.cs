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
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Linting.UndefinedVariables {
    internal sealed class NameCollectorWalker : PythonWalker {
        private readonly HashSet<string> _names;
        private readonly HashSet<NameExpression> _additionalNameNodes;

        public NameCollectorWalker(HashSet<string> names, HashSet<NameExpression> additionalNameNodes) {
            _names = names;
            _additionalNameNodes = additionalNameNodes;
        }

        public override bool Walk(NameExpression nex) {
            if (!string.IsNullOrEmpty(nex.Name)) {
                _names.Add(nex.Name);
                _additionalNameNodes.Add(nex);
            }
            return false;
        }
    }
}
