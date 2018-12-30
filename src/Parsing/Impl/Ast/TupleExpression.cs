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

using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Core;

namespace Microsoft.Python.Parsing.Ast {
    public class TupleExpression : SequenceExpression {
        public TupleExpression(bool expandable, params Expression[] items)
            : base(items) {
            IsExpandable = expandable;
        }

        internal override string CheckAssign() {
            if (Items.Count == 0) {
                return "can't assign to ()";
            }
            return base.CheckAssign();
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                foreach (var e in Items.MaybeEnumerate()) {
                    e.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                foreach (var e in Items.MaybeEnumerate()) {
                    await e.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        public bool IsExpandable { get; }

        /// <summary>
        /// Marks this tuple expression as having no parenthesis for the purposes of round tripping.
        /// </summary>
        public void RoundTripHasNoParenthesis(PythonAst ast) => ast.SetAttribute(this, NodeAttributes.IsAltFormValue, NodeAttributes.IsAltFormValue);

        public override string GetLeadingWhiteSpace(PythonAst ast) {
            if (this.IsAltForm(ast)) {
                return Items[0].GetLeadingWhiteSpace(ast);
            }
            return base.GetLeadingWhiteSpace(ast);
        }

        public override void SetLeadingWhiteSpace(PythonAst ast, string whiteSpace) {
            if (this.IsAltForm(ast)) {
                Items[0].SetLeadingWhiteSpace(ast, whiteSpace);
            } else {
                base.SetLeadingWhiteSpace(ast, whiteSpace);
            }
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            var hasOpenSquareBracket = res.Length > 0 && res[res.Length - 1] == '['; // Tuple[
            if (this.IsAltForm(ast)) {
                ListExpression.AppendItems(res, ast, format, string.Empty, string.Empty, this, Items);
            } else {
                if (Items.Count == 0 && format.SpaceWithinEmptyTupleExpression != null) {
                    format.ReflowComment(res, this.GetPreceedingWhiteSpace(ast));
                    res.AppendIf(!hasOpenSquareBracket, '(');
                    format.Append(res, format.SpaceWithinEmptyTupleExpression, " ", string.Empty, this.GetSecondWhiteSpaceDefaultNull(ast));
                    res.AppendIf(!hasOpenSquareBracket, ')');
                } else {
                    ListExpression.AppendItems(res, ast, format,
                        !hasOpenSquareBracket ? "(" : string.Empty, 
                        this.IsMissingCloseGrouping(ast) ? string.Empty :
                            (!hasOpenSquareBracket ? ")" : string.Empty), 
                        this, 
                        Items, 
                        format.SpacesWithinParenthesisedTupleExpression);
                }
            }
        }
    }
}
