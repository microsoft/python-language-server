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
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Python.Parsing.Ast {
    [DebuggerDisplay("{Name}")]
    public class FunctionDefinition : ScopeStatement, IMaybeAsyncStatement {
        internal static readonly object WhitespaceAfterAsync = new object();

        private int? _keywordEndIndex;

        protected Statement _body;

        public FunctionDefinition(NameExpression name, Parameter[] parameters)
            : this(name, parameters, null) { }

        public FunctionDefinition(NameExpression name, Parameter[] parameters, Statement body, DecoratorStatement decorators = null) {
            if (name == null) {
                NameExpression = new NameExpression("<lambda>");
                IsLambda = true;
            } else {
                NameExpression = name;
            }

            Parameters = parameters ?? Array.Empty<Parameter>();
            _body = body;
            Decorators = decorators;
            ScopeInfo = new FunctionScopeInfo(this);
        }

        public bool IsLambda { get; }

        public Parameter[] Parameters { get; }

        internal void SetKeywordEndIndex(int index) => _keywordEndIndex = index;
        public override int KeywordEndIndex => _keywordEndIndex ?? (DefIndex + (IsCoroutine ? 9 : 3));
        public override int KeywordLength => KeywordEndIndex - StartIndex;

        public Expression ReturnAnnotation { get; set; }

        public int HeaderIndex { get; set; }

        public int DefIndex { get; set; }

        public NameExpression NameExpression { get; }

        public DecoratorStatement Decorators { get; internal set; }

        public LambdaExpression LambdaExpression { get; set; }

        /// <summary>
        /// True if the function is a generator.  Generators contain at least one yield
        /// expression and instead of returning a value when called they return a generator
        /// object which implements the iterator protocol.
        /// </summary>
        public bool IsGenerator { get; set; }

        /// <summary>
        /// True if the function is a coroutine. Coroutines are defined using
        /// 'async def'.
        /// </summary>
        public bool IsCoroutine { get; set; }

        bool IMaybeAsyncStatement.IsAsync => IsCoroutine;

        /// <summary>
        /// Gets the variable that this function is assigned to.
        /// </summary>
        public PythonVariable Variable { get; set; }

        #region IScopeStatement
        public override Statement Body => _body;
        internal void SetBody(Statement body) => _body = body;
        #endregion

        #region ScopeStatement
        public override string Name => NameExpression.Name ?? string.Empty;
        internal override ScopeInfo ScopeInfo { get; }
        #endregion

        public int GetIndexOfDef(PythonAst ast) {
            if (!IsCoroutine) {
                return DefIndex;
            }
            return DefIndex + NodeAttributes.GetWhiteSpace(this, ast, WhitespaceAfterAsync).Length + 5;
        }

        public override IEnumerable<Node> GetChildNodes() {
            if (NameExpression != null) yield return NameExpression;
            foreach (var parameter in Parameters) {
                yield return parameter;
            }
            if (Decorators != null) yield return Decorators;
            if (_body != null) yield return _body;
            if (ReturnAnnotation != null) yield return ReturnAnnotation;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                NameExpression?.Walk(walker);
                foreach (var p in Parameters) {
                    p.Walk(walker);
                }
                Decorators?.Walk(walker);
                _body?.Walk(walker);
                ReturnAnnotation?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                if (NameExpression != null) {
                    await NameExpression.WalkAsync(walker, cancellationToken);
                }
                foreach (var p in Parameters) {
                    await p.WalkAsync(walker, cancellationToken);
                }
                if (Decorators != null) {
                    await Decorators.WalkAsync(walker, cancellationToken);
                }
                if (_body != null) {
                    await _body.WalkAsync(walker, cancellationToken);
                }
                if (ReturnAnnotation != null) {
                    await ReturnAnnotation.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        public override string GetLeadingWhiteSpace(PythonAst ast) {
            if (Decorators != null) {
                return Decorators.GetLeadingWhiteSpace(ast);
            }

            return base.GetLeadingWhiteSpace(ast);
        }

        public override void SetLeadingWhiteSpace(PythonAst ast, string whiteSpace) {
            if (Decorators != null) {
                Decorators.SetLeadingWhiteSpace(ast, whiteSpace);
                return;
            }
            base.SetLeadingWhiteSpace(ast, whiteSpace);
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            Decorators?.AppendCodeString(res, ast, format);

            format.ReflowComment(res, this.GetPreceedingWhiteSpaceDefaultNull(ast));

            if (IsCoroutine) {
                res.Append("async");
                res.Append(NodeAttributes.GetWhiteSpace(this, ast, WhitespaceAfterAsync));
            }

            res.Append("def");
            var name = this.GetVerbatimImage(ast) ?? Name;
            if (!string.IsNullOrEmpty(name)) {
                res.Append(this.GetSecondWhiteSpace(ast));
                res.Append(name);
                if (!this.IsIncompleteNode(ast)) {
                    format.Append(
                        res,
                        format.SpaceBeforeFunctionDeclarationParen,
                        " ",
                        "",
                        this.GetThirdWhiteSpaceDefaultNull(ast)
                    );

                    res.Append('(');
                    if (Parameters.Length != 0) {
                        var commaWhiteSpace = this.GetListWhiteSpace(ast);
                        ParamsToString(res,
                            ast,
                            commaWhiteSpace,
                            format,
                            format.SpaceWithinFunctionDeclarationParens != null ? format.SpaceWithinFunctionDeclarationParens.Value ? " " : "" : null
                        );
                    }

                    var namedOnly = this.GetExtraVerbatimText(ast);
                    if (namedOnly != null) {
                        res.Append(namedOnly);
                    }

                    format.Append(
                        res,
                        Parameters.Length != 0 ?
                            format.SpaceWithinFunctionDeclarationParens :
                            format.SpaceWithinEmptyParameterList,
                        " ",
                        "",
                        this.GetFourthWhiteSpaceDefaultNull(ast)
                    );

                    if (!this.IsMissingCloseGrouping(ast)) {
                        res.Append(')');
                    }

                    if (ReturnAnnotation != null) {
                        format.Append(
                            res,
                            format.SpaceAroundAnnotationArrow,
                            " ",
                            string.Empty,
                            this.GetFifthWhiteSpace(ast)
                        );
                        res.Append("->");
                        ReturnAnnotation.AppendCodeString(
                            res,
                            ast,
                            format,
                            format.SpaceAroundAnnotationArrow != null ? format.SpaceAroundAnnotationArrow.Value ? " " : string.Empty : null
                        );
                    }

                    Body?.AppendCodeString(res, ast, format);
                }
            }
        }

        internal void ParamsToString(StringBuilder res, PythonAst ast, string[] commaWhiteSpace, CodeFormattingOptions format, string initialLeadingWhiteSpace = null) {
            for (var i = 0; i < Parameters.Length; i++) {
                if (i > 0) {
                    if (commaWhiteSpace != null) {
                        res.Append(commaWhiteSpace[i - 1]);
                    }

                    res.Append(',');
                }

                Parameters[i].AppendCodeString(res, ast, format, initialLeadingWhiteSpace);
                initialLeadingWhiteSpace = null;
            }
            if (commaWhiteSpace != null && commaWhiteSpace.Length == Parameters.Length && Parameters.Length != 0) {
                // trailing comma
                res.Append(commaWhiteSpace[commaWhiteSpace.Length - 1]);
                res.Append(",");
            }
        }
    }
}
