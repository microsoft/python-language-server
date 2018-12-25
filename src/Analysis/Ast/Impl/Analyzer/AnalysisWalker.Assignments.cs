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

using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Analysis.Types;
using Microsoft.Python.Parsing.Ast;

namespace Microsoft.Python.Analysis.Analyzer {
    internal partial class AnalysisWalker {
        public override async Task<bool> WalkAsync(AssignmentStatement node, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            var value = await Lookup.GetValueFromExpressionAsync(node.Right, cancellationToken);

            if (value == null || value.MemberType == PythonMemberType.Unknown) {
                Log?.Log(TraceEventType.Verbose, $"Undefined value: {node.Right.ToCodeString(Ast).Trim()}");
            }

            if (value?.GetPythonType().TypeId == BuiltinTypeId.Ellipsis) {
                value = Lookup.UnknownType;
            }

            if (node.Left.FirstOrDefault() is TupleExpression tex) {
                // Tuple = Tuple. Transfer values.
                var texHandler = new TupleExpressionHandler(Lookup);
                await texHandler.HandleTupleAssignmentAsync(tex, node.Right, value, cancellationToken);
                return await base.WalkAsync(node, cancellationToken);
            }

            foreach (var expr in node.Left.OfType<ExpressionWithAnnotation>()) {
                AssignFromAnnotation(expr);
                if (!value.IsUnknown() && expr.Expression is NameExpression ne) {
                    Lookup.DeclareVariable(ne.Name, value, GetLoc(ne));
                }
            }

            foreach (var ne in node.Left.OfType<NameExpression>()) {
                Lookup.DeclareVariable(ne.Name, value, GetLoc(ne));
            }

            return await base.WalkAsync(node, cancellationToken);
        }

        public override Task<bool> WalkAsync(ExpressionStatement node, CancellationToken cancellationToken = default) {
            AssignFromAnnotation(node.Expression as ExpressionWithAnnotation);
            return Task.FromResult(false);
        }

        private void AssignFromAnnotation(ExpressionWithAnnotation expr) {
            if (expr?.Annotation != null && expr.Expression is NameExpression ne) {
                var t = Lookup.GetTypeFromAnnotation(expr.Annotation);
                Lookup.DeclareVariable(ne.Name, t ?? Lookup.UnknownType, GetLoc(expr.Expression));
            }
        }
    }
}
