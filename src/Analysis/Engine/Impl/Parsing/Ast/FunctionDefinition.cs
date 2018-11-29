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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Diagnostics;
using System.Text;
using Microsoft.Python.Core;

namespace Microsoft.PythonTools.Parsing.Ast {
    [DebuggerDisplay("{Name}")]
    public class FunctionDefinition : ScopeStatement, IMaybeAsyncStatement {
        internal static readonly object WhitespaceAfterAsync = new object();

        private readonly Parameter[] _parameters;
        private int? _keywordEndIndex;

        protected Statement _body;

        public FunctionDefinition(NameExpression name, Parameter[] parameters)
            : this(name, parameters, (Statement)null) {
        }

        public FunctionDefinition(NameExpression name, Parameter[] parameters, Statement body, DecoratorStatement decorators = null) {
            if (name == null) {
                NameExpression = new NameExpression("<lambda>");
                IsLambda = true;
            } else {
                NameExpression = name;
            }

            _parameters = parameters;
            _body = body;
            Decorators = decorators;
        }

        public bool IsLambda { get; }

        public Parameter[] Parameters => _parameters ?? Array.Empty<Parameter>();

        internal override int ArgCount => Parameters.Length;

        internal void SetKeywordEndIndex(int index) => _keywordEndIndex = index;
        public override int KeywordEndIndex => _keywordEndIndex ?? (DefIndex + (IsCoroutine ? 9 : 3));
        public override int KeywordLength => KeywordEndIndex - StartIndex;

        public Expression ReturnAnnotation { get; set; }

        public override Statement Body => _body;

        internal void SetBody(Statement body) => _body = body;

        public int HeaderIndex { get; set; }

        public int DefIndex { get; set; }

        public override string/*!*/ Name => NameExpression.Name ?? "";

        public NameExpression NameExpression { get; }

        public DecoratorStatement Decorators { get; internal set; }

        internal LambdaExpression LambdaExpression { get; set; }

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

        /// <summary>
        /// Gets the variable reference for the specific assignment to the variable for this function definition.
        /// </summary>
        public PythonReference GetVariableReference(PythonAst ast) {
            return GetVariableReference(this, ast);
        }

        internal override bool ExposesLocalVariable(PythonVariable variable) {
            return NeedsLocalsDictionary;
        }

        internal override bool TryBindOuter(ScopeStatement from, string name, bool allowGlobals, out PythonVariable variable) {
            // Functions expose their locals to direct access
            ContainsNestedFreeVariables = true;
            if (TryGetVariable(name, out variable)) {
                variable.AccessedInNestedScope = true;

                if (variable.Kind == VariableKind.Local || variable.Kind == VariableKind.Parameter) {
                    from.AddFreeVariable(variable, true);

                    for (ScopeStatement scope = from.Parent; scope != this; scope = scope.Parent) {
                        scope.AddFreeVariable(variable, false);
                    }

                    AddCellVariable(variable);
                } else if (allowGlobals) {
                    from.AddReferencedGlobal(name);
                }
                return true;
            }
            return false;
        }

        internal override PythonVariable BindReference(PythonNameBinder binder, string name) {
            PythonVariable variable;

            // First try variables local to this scope
            if (TryGetVariable(name, out variable) && variable.Kind != VariableKind.Nonlocal) {
                if (variable.Kind == VariableKind.Global) {
                    AddReferencedGlobal(name);
                }
                return variable;
            }

            // Try to bind in outer scopes
            for (ScopeStatement parent = Parent; parent != null; parent = parent.Parent) {
                if (parent.TryBindOuter(this, name, true, out variable)) {
                    return variable;
                }
            }

            return null;
        }


        internal override void Bind(PythonNameBinder binder) {
            base.Bind(binder);
            Verify(binder);
        }

        private void Verify(PythonNameBinder binder) {
            if (ContainsImportStar && IsClosure) {
                binder.ReportSyntaxError(
                    "import * is not allowed in function '{0}' because it is a nested function".FormatUI(Name),
                    this);
            }
            if (ContainsImportStar && Parent is FunctionDefinition) {
                binder.ReportSyntaxError(
                    "import * is not allowed in function '{0}' because it is a nested function".FormatUI(Name),
                    this);
            }
            if (ContainsImportStar && ContainsNestedFreeVariables) {
                binder.ReportSyntaxError(
                    "import * is not allowed in function '{0}' because it contains a nested function with free variables".FormatUI(Name),
                    this);
            }
            if (ContainsUnqualifiedExec && ContainsNestedFreeVariables) {
                binder.ReportSyntaxError(
                    "unqualified exec is not allowed in function '{0}' because it contains a nested function with free variables".FormatUI(Name),
                    this);
            }
            if (ContainsUnqualifiedExec && IsClosure) {
                binder.ReportSyntaxError(
                    "unqualified exec is not allowed in function '{0}' because it is a nested function".FormatUI(Name),
                    this);
            }
        }

        public int GetIndexOfDef(PythonAst ast) {
            if (!IsCoroutine) {
                return DefIndex;
            }
            return DefIndex + NodeAttributes.GetWhiteSpace(this, ast, WhitespaceAfterAsync).Length + 5;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                NameExpression?.Walk(walker);
                foreach (var p in _parameters.MaybeEnumerate()) {
                    p.Walk(walker);
                }
                Decorators?.Walk(walker);
                _body?.Walk(walker);
                ReturnAnnotation?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        public SourceLocation Header {
            get { return GlobalParent.IndexToLocation(HeaderIndex); }
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
                            format.SpaceWithinFunctionDeclarationParens != null ?
                                format.SpaceWithinFunctionDeclarationParens.Value ? " " : "" :
                                null
                        );
                    }

                    string namedOnly = this.GetExtraVerbatimText(ast);
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
                            "",
                            this.GetFifthWhiteSpace(ast)
                        );
                        res.Append("->");
                        ReturnAnnotation.AppendCodeString(
                            res,
                            ast,
                            format,
                            format.SpaceAroundAnnotationArrow != null ?
                                format.SpaceAroundAnnotationArrow.Value ? " " : "" :
                                null
                        );
                    }

                    Body?.AppendCodeString(res, ast, format);
                }
            }
        }

        internal void ParamsToString(StringBuilder res, PythonAst ast, string[] commaWhiteSpace, CodeFormattingOptions format, string initialLeadingWhiteSpace = null) {
            for (int i = 0; i < Parameters.Length; i++) {
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
