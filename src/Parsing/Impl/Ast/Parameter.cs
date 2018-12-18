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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Python.Core.Text;

namespace Microsoft.Python.Parsing.Ast {
    /// <summary>
    /// Parameter base class
    /// </summary>
    public class Parameter : Node {
        internal static readonly object WhitespacePrecedingAssign = new object();

        public Parameter(NameExpression name, ParameterKind kind) {
            NameExpression = name;
            Kind = kind;
        }

        public override string NodeName => "parameter name";

        /// <summary>
        /// Parameter name
        /// </summary>
        public virtual string/*!*/ Name => NameExpression?.Name ?? string.Empty;

        public NameExpression NameExpression { get; }
        internal IndexSpan NameSpan => NameExpression?.IndexSpan ?? IndexSpan;

        public Expression DefaultValue { get; set; }

        public Expression Annotation { get; set; }

        public bool IsList => Kind == ParameterKind.List;

        public bool IsDictionary => Kind == ParameterKind.Dictionary;

        public bool IsKeywordOnly => Kind == ParameterKind.KeywordOnly;

        public ParameterKind Kind { get; }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Annotation?.Walk(walker);
                DefaultValue?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                if (Annotation != null) {
                    await Annotation.WalkAsync(walker, cancellationToken);
                }
                if (DefaultValue != null) {
                    await DefaultValue.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        public PythonVariable GetVariable(PythonAst ast)
            => ast.TryGetAttribute(this, NodeAttributes.Variable, out var reference) ? (PythonVariable)reference : null;

        public void AddPreceedingWhiteSpace(PythonAst ast, string whiteSpace) 
            => ast.SetAttribute(this, NodeAttributes.PreceedingWhiteSpace, whiteSpace);

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) 
            => AppendCodeString(res, ast, format, null);

        internal virtual void AppendParameterName(StringBuilder res, PythonAst ast, CodeFormattingOptions format, string leadingWhiteSpace) 
            => NameExpression?.AppendCodeString(res, ast, format, leadingWhiteSpace);

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format, string leadingWhiteSpace) {
            var kwOnlyText = this.GetExtraVerbatimText(ast);
            if (kwOnlyText != null) {
                if (leadingWhiteSpace != null) {
                    res.Append(leadingWhiteSpace);
                    res.Append(kwOnlyText.TrimStart());
                    leadingWhiteSpace = null;
                } else {
                    res.Append(kwOnlyText);
                }
            }

            var writeName = true;
            switch (Kind) {
                case ParameterKind.Dictionary:
                    res.Append(leadingWhiteSpace ?? this.GetPreceedingWhiteSpaceDefaultNull(ast) ?? "");
                    leadingWhiteSpace = null;
                    res.Append("**");
                    break;
                case ParameterKind.List:
                    res.Append(leadingWhiteSpace ?? this.GetPreceedingWhiteSpaceDefaultNull(ast) ?? "");
                    leadingWhiteSpace = null;
                    res.Append('*');
                    break;
                case ParameterKind.Normal:
                    if (this.IsAltForm(ast)) {
                        res.Append(leadingWhiteSpace ?? this.GetPreceedingWhiteSpaceDefaultNull(ast) ?? "");
                        leadingWhiteSpace = null;
                        res.Append('(');
                        AppendParameterName(res, ast, format, leadingWhiteSpace);
                        if (!this.IsMissingCloseGrouping(ast)) {
                            res.Append(this.GetSecondWhiteSpace(ast));
                            res.Append(')');
                        }
                        writeName = false;
                    }
                    break;
                case ParameterKind.KeywordOnly:
                    break;
                default: throw new InvalidOperationException();
            }

            if (writeName) {
                AppendParameterName(res, ast, format, leadingWhiteSpace);
            }

            if (Annotation != null) {
                res.Append(this.GetThirdWhiteSpaceDefaultNull(ast) ?? "");
                res.Append(':');
                Annotation.AppendCodeString(res, ast, format);
            }

            if (DefaultValue != null) {
                format.Append(
                    res,
                    format.SpaceAroundDefaultValueEquals,
                    " ",
                    "",
                    NodeAttributes.GetWhiteSpace(this, ast, WhitespacePrecedingAssign)
                );

                res.Append('=');
                if (format.SpaceAroundDefaultValueEquals != null) {
                    DefaultValue.AppendCodeString(res, ast, format, format.SpaceAroundDefaultValueEquals.Value ? " " : "");
                } else {
                    DefaultValue.AppendCodeString(res, ast, format);
                }
            }
        }
    }

}
