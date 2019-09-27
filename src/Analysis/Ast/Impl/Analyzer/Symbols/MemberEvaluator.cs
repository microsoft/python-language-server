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
using System.Diagnostics;
using Microsoft.Python.Analysis.Analyzer.Evaluation;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing.Ast;
using Microsoft.Python.Parsing.Definition;

namespace Microsoft.Python.Analysis.Analyzer.Symbols {
    [DebuggerDisplay("{Target.Name}")]
    internal abstract class MemberEvaluator : AnalysisWalker {
        protected MemberEvaluator(ExpressionEval eval, IScopeNode target) : base(eval) {
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public IScopeNode Target { get; }
        public IMember Result { get; protected set; }
        public abstract void Evaluate();
    }
}
