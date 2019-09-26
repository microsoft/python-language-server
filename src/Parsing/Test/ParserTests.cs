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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using FluentAssertions;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Core.Text;
using Microsoft.Python.Parsing.Ast;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Parsing.Tests {
    /// <summary>
    /// Test cases for parser written in a continuation passing style.
    /// </summary>
    [TestClass]
    public class ParserTests {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void TestCleanup() => TestEnvironmentImpl.TestCleanup();

        internal static readonly PythonLanguageVersion[] AllVersions = new[] { PythonLanguageVersion.V26, PythonLanguageVersion.V27, PythonLanguageVersion.V30, PythonLanguageVersion.V31, PythonLanguageVersion.V32, PythonLanguageVersion.V33, PythonLanguageVersion.V34, PythonLanguageVersion.V35, PythonLanguageVersion.V36, PythonLanguageVersion.V37, PythonLanguageVersion.V38 };
        internal static readonly PythonLanguageVersion[] V26AndUp = AllVersions.Where(v => v >= PythonLanguageVersion.V26).ToArray();
        internal static readonly PythonLanguageVersion[] V27AndUp = AllVersions.Where(v => v >= PythonLanguageVersion.V27).ToArray();
        internal static readonly PythonLanguageVersion[] V2Versions = AllVersions.Where(v => v <= PythonLanguageVersion.V27).ToArray();
        internal static readonly PythonLanguageVersion[] V24_V26Versions = AllVersions.Where(v => v <= PythonLanguageVersion.V26).ToArray();
        internal static readonly PythonLanguageVersion[] V24_V25Versions = AllVersions.Where(v => v <= PythonLanguageVersion.V25).ToArray();
        internal static readonly PythonLanguageVersion[] V25_V27Versions = AllVersions.Where(v => v >= PythonLanguageVersion.V25 && v <= PythonLanguageVersion.V27).ToArray();
        internal static readonly PythonLanguageVersion[] V26_V27Versions = AllVersions.Where(v => v >= PythonLanguageVersion.V26 && v <= PythonLanguageVersion.V27).ToArray();
        internal static readonly PythonLanguageVersion[] V30_V32Versions = AllVersions.Where(v => v >= PythonLanguageVersion.V30 && v <= PythonLanguageVersion.V32).ToArray();
        internal static readonly PythonLanguageVersion[] V3Versions = AllVersions.Where(v => v >= PythonLanguageVersion.V30).ToArray();
        internal static readonly PythonLanguageVersion[] V33AndV34 = AllVersions.Where(v => v >= PythonLanguageVersion.V33 && v <= PythonLanguageVersion.V34).ToArray();
        internal static readonly PythonLanguageVersion[] V33AndUp = AllVersions.Where(v => v >= PythonLanguageVersion.V33).ToArray();
        internal static readonly PythonLanguageVersion[] V35AndUp = AllVersions.Where(v => v >= PythonLanguageVersion.V35).ToArray();
        internal static readonly PythonLanguageVersion[] V36AndUp = AllVersions.Where(v => v >= PythonLanguageVersion.V36).ToArray();
        internal static readonly PythonLanguageVersion[] V37AndUp = AllVersions.Where(v => v >= PythonLanguageVersion.V37).ToArray();
        internal static readonly PythonLanguageVersion[] V38AndUp = AllVersions.Where(v => v >= PythonLanguageVersion.V38).ToArray();

        #region Test Cases

        [TestMethod, Priority(0)]
        public void MixedWhiteSpace() {
            // mixed, but in different blocks, which is ok
            ParseFileNoErrors("MixedWhitespace1.py", PythonLanguageVersion.V27, Severity.Error);

            // mixed in the same block, tabs first
            ParseErrors("MixedWhitespace2.py", PythonLanguageVersion.V27, Severity.Error, new ErrorResult("inconsistent whitespace", new SourceSpan(14, 1, 14, 9)));

            // mixed in same block, spaces first
            ParseErrors("MixedWhitespace3.py", PythonLanguageVersion.V27, Severity.Error, new ErrorResult("inconsistent whitespace", new SourceSpan(14, 1, 14, 2)));

            // mixed on same line, spaces first
            ParseFileNoErrors("MixedWhitespace4.py", PythonLanguageVersion.V27, Severity.Error);

            // mixed on same line, tabs first
            ParseFileNoErrors("MixedWhitespace5.py", PythonLanguageVersion.V27, Severity.Error);

            // mixed on a comment line - should not crash
            ParseErrors("MixedWhitespace6.py", PythonLanguageVersion.V27, Severity.Error, new ErrorResult("inconsistent whitespace", new SourceSpan(9, 1, 9, 2)));
        }

        [TestMethod, Priority(0)]
        public void Errors35() {
            ParseErrors("Errors35.py",
                PythonLanguageVersion.V35,
                    new ErrorResult("iterable unpacking cannot be used in comprehension", new SourceSpan(1, 2, 1, 5)),
                    new ErrorResult("invalid syntax", new SourceSpan(3, 11, 3, 14)),
                    new ErrorResult("can't use starred expression here", new SourceSpan(4, 2, 4, 5)),
                    new ErrorResult("invalid syntax", new SourceSpan(5, 7, 5, 11)),
                    new ErrorResult("invalid syntax", new SourceSpan(6, 8, 6, 11)),
                    new ErrorResult("iterable argument unpacking follows keyword argument unpacking", new SourceSpan(7, 9, 7, 12)),
                    new ErrorResult("invalid syntax", new SourceSpan(8, 8, 8, 10)),
                    new ErrorResult("invalid syntax", new SourceSpan(9, 6, 9, 10)),
                    new ErrorResult("invalid syntax", new SourceSpan(10, 2, 10, 8))
            );
        }

        [TestMethod, Priority(0)]
        public void FStringErrors() {
            ParseErrors("FStringErrors.py",
                PythonLanguageVersion.V36,
                new ErrorResult("f-string: expecting '}'", new SourceSpan(1, 4, 1, 5)),
                new ErrorResult("f-string expression part cannot include a backslash", new SourceSpan(2, 4, 2, 5)),
                new ErrorResult("unexpected token 'import'", new SourceSpan(4, 2, 4, 8)),
                new ErrorResult("unexpected token 'def'", new SourceSpan(7, 2, 7, 5)),
                new ErrorResult("cannot mix bytes and nonbytes literals", new SourceSpan(11, 5, 11, 8)),
                new ErrorResult("f-string: expecting '}'", new SourceSpan(13, 13, 13, 14)),
                new ErrorResult("f-string: single '}' is not allowed", new SourceSpan(15, 3, 15, 4)),
                new ErrorResult("f-string expression part cannot include '#'", new SourceSpan(17, 4, 17, 5)),
                new ErrorResult("f-string: expecting '}'", new SourceSpan(19, 4, 19, 5)),
                new ErrorResult("unexpected token 'import'", new SourceSpan(21, 4, 21, 10)),
                new ErrorResult("f-string: empty expression not allowed", new SourceSpan(23, 4, 23, 5)),
                new ErrorResult("unexpected token '='", new SourceSpan(25, 6, 25, 7)),
                new ErrorResult("expected ':'", new SourceSpan(27, 12, 27, 12)),
                new ErrorResult("f-string: lambda must be inside parentheses", new SourceSpan(27, 4, 27, 12)),
                new ErrorResult("f-string: expecting '}'", new SourceSpan(29, 6, 29, 7)),
                new ErrorResult("f-string: invalid conversion character: expected 's', 'r', or 'a'", new SourceSpan(31, 6, 31, 7)),
                new ErrorResult("f-string: invalid conversion character: k expected 's', 'r', or 'a'", new SourceSpan(33, 7, 33, 8)),
                new ErrorResult("f-string: unmatched ')'", new SourceSpan(35, 4, 35, 5)),
                new ErrorResult("f-string: unmatched ')'", new SourceSpan(37, 6, 37, 7)),
                new ErrorResult("f-string: closing parenthesis '}' does not match opening parenthesis '('", new SourceSpan(39, 6, 39, 7)),
                new ErrorResult("f-string: unmatched ']'", new SourceSpan(41, 4, 41, 5))
            );
        }

        [DataRow("True", ParseResult.Complete)]
        [DataRow("if True:", ParseResult.IncompleteStatement)]
        [DataRow("if True", ParseResult.Invalid)]
        [DataRow("", ParseResult.Empty)]
        [DataTestMethod, Priority(0)]
        public void InteractiveCode(string code, ParseResult expectedResult) {
            foreach (var version in AllVersions) {
                var module = new Uri("file:///interactive");
                var parser = Parser.CreateParser(new StringReader(code), version);
                var ast = parser.ParseInteractiveCode(module, out var result);
                result.Should().Be(expectedResult);
                if (expectedResult == ParseResult.Complete) {
                    ast.Should().NotBeNull();
                    ast.Module.Should().BeEquivalentTo(module);
                } else {
                    ast.Should().BeNull();
                }
            }
        }

        [TestMethod, Priority(0)]
        public void FStrings() {
            foreach (var version in V36AndUp) {
                var errors = new CollectingErrorSink();
                CheckAst(
                    ParseFile("FStrings.py", errors, version),
                    CheckSuite(
                        CheckAssignment(
                            CheckNameExpr("some"),
                            One
                        ),
                        CheckExprStmt(
                            CheckFString(
                                CheckNodeConstant("text")
                            )
                        ),
                        CheckExprStmt(
                            CheckFString(
                                CheckFormattedValue(
                                    CheckNameExpr("some")
                                )
                            )
                        ),
                        CheckFuncDef("f", NoParameters, CheckSuite(
                            CheckReturnStmt(
                                CheckFString(
                                    CheckFormattedValue(
                                        CheckYieldExpr(CheckNameExpr("some"))
                                    )
                                )
                           )
                        )),
                        CheckExprStmt(
                            CheckFString(
                                CheckNodeConstant("result: "),
                                CheckFormattedValue(
                                    CheckFString(
                                        CheckFormattedValue(
                                            CheckNameExpr("some")
                                        )
                                    )
                                )
                            )
                        ),
                        CheckExprStmt(
                            CheckFString(
                                CheckNodeConstant("{{text "),
                                CheckFormattedValue(
                                    CheckNameExpr("some")
                                ),
                                CheckNodeConstant(" }}")
                            )
                       ),
                       CheckExprStmt(
                           CheckFString(
                               CheckFormattedValue(
                                   CheckBinaryExpression(
                                       CheckNameExpr("some"),
                                        PythonOperator.Add,
                                        One
                                   )
                               )
                           )
                       ),
                       CheckExprStmt(
                           CheckFString(
                               CheckNodeConstant("Has a :")
                           )
                       ),
                       CheckExprStmt(
                           CheckFString(
                               CheckFormattedValue(
                                   One,
                                   null,
                                   CheckFormatSpecifer(
                                        CheckFormattedValue(
                                            CheckConstant("{")
                                        ),
                                        CheckNodeConstant(">10")
                                   )
                               )
                           )
                        ),
                        CheckExprStmt(
                            CheckFString(
                                CheckFormattedValue(
                                    CheckNameExpr("some")
                                )
                            )
                        ),
                        CheckExprStmt(
                            CheckFString(
                                CheckNodeConstant("\n")
                            )
                        ),
                        CheckExprStmt(
                            CheckFString(
                                CheckNodeConstant("space between opening braces: "),
                                CheckFormattedValue(
                                    CheckSetComp(
                                        CheckNameExpr("thing"),
                                        CompFor(
                                            CheckNameExpr("thing"),
                                            CheckTupleExpr(
                                                One,
                                                CheckConstant(2),
                                                CheckConstant(3)
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                        CheckExprStmt(
                            CheckCallExpression(
                                CheckNameExpr("print"),
                                PositionalArg(
                                    CheckFString(
                                        CheckNodeConstant("first: "),
                                        CheckFormattedValue(
                                            CheckFString(
                                                CheckNodeConstant("second "),
                                                CheckFormattedValue(
                                                    CheckNameExpr("some")
                                                )
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                        CheckExprStmt(
                            CheckFString(
                                CheckFormattedValue(
                                    CheckNameExpr("some"),
                                    'r',
                                    CheckFormatSpecifer(
                                        CheckFormattedValue(
                                            CheckNameExpr("some")
                                        )
                                    )
                                )
                            )
                        ),
                        CheckExprStmt(
                            CheckFString(
                                CheckFormattedValue(
                                    CheckNameExpr("some"),
                                    null,
                                    CheckFormatSpecifer(
                                        CheckNodeConstant("#06x")
                                    )
                                )
                            )
                       ),
                       CheckExprStmt(
                            CheckFString(
                                CheckNodeConstant("\n"),
                                CheckFormattedValue(
                                    One
                                ),
                                CheckNodeConstant("\n")
                            )
                       ),
                       CheckExprStmt(
                            CheckFString(
                                CheckNodeConstant("{{nothing")
                            )
                       ),
                       CheckExprStmt(
                            CheckFString(
                                CheckNodeConstant("Hello '"),
                                CheckFormattedValue(
                                    CheckBinaryExpression(
                                        CheckFString(
                                            CheckFormattedValue(
                                                CheckNameExpr("some")
                                            )
                                        ),
                                        PythonOperator.Add,
                                        CheckConstant("example")
                                    )
                                )
                            )
                       ),
                       CheckExprStmt(
                            CheckFString(
                                CheckFormattedValue(
                                    CheckConstant(3.14),
                                    null,
                                    CheckFormatSpecifer(
                                        CheckNodeConstant("!<10.10")
                                    )
                                )
                            )
                        ),
                        CheckExprStmt(
                            CheckFString(
                                CheckFormattedValue(
                                    CheckBinaryExpression(
                                        One,
                                        PythonOperator.Add,
                                        One
                                    )
                                )
                            )
                        ),
                        CheckExprStmt(
                            CheckFString(
                                CheckNodeConstant("\\N{GREEK CAPITAL LETTER DELTA}")
                            )
                        ),
                        CheckExprStmt(
                            CheckFString(
                                CheckNodeConstant("\\"),
                                CheckFormattedValue(
                                    One
                                )
                            )
                        ),
                        CheckExprStmt(
                            CheckFString(
                                CheckFormattedValue(
                                    CheckNameExpr("a"),
                                    null,
                                    CheckFormatSpecifer(
                                        CheckNodeConstant("{{}}")
                                    )
                                )
                            )
                        ),
                        CheckExprStmt(
                            CheckFString(
                                CheckFormattedValue(
                                    CheckCallExpression(
                                        CheckParenExpr(
                                            CheckLambda(
                                                new[] { CheckParameter("x") },
                                                CheckBinaryExpression(
                                                    CheckNameExpr("x"),
                                                    PythonOperator.Add,
                                                    One
                                                )
                                            )
                                        ),
                                        PositionalArg(One)
                                    )
                                )
                            )
                        ),
                        CheckExprStmt(
                            CheckFString(
                                CheckFormattedValue(
                                    CheckIndexExpression(
                                        CheckListExpr(One, Two, One),
                                        CheckSlice(One, null)
                                    )
                                )
                            )
                        )
                    )
                );

                errors.Errors.Should().BeEmpty();
            }
        }

        [TestMethod, Priority(0)]
        public void FStringEquals() {
            foreach (var version in V38AndUp) {
                var errors = new CollectingErrorSink();
                CheckAst(
                    ParseFile("FStringEquals.py", errors, version),
                    CheckSuite(
                        CheckExprStmt(
                            CheckFString(
                                CheckFormattedValue(
                                    CheckNameExpr("name")
                                )
                            )
                        ),
                        CheckExprStmt(
                            CheckFString(
                                CheckFormattedValue(
                                    CheckNameExpr("name")
                                )
                            )
                        ),
                        CheckExprStmt(
                            CheckFString(
                                CheckFormattedValue(
                                    CheckNameExpr("name")
                                )
                            )
                        ),
                        CheckExprStmt(
                            CheckFString(
                                CheckFormattedValue(
                                    CheckCallExpression(CheckMemberExpr(CheckNameExpr("foo"), "bar"))
                                )
                            )
                        ),
                        CheckExprStmt(
                            CheckFString(
                                CheckFormattedValue(
                                    CheckNameExpr("user"),
                                    's'
                                ),
                                CheckNodeConstant("  "),
                                CheckFormattedValue(
                                    CheckMemberExpr(CheckNameExpr("delta"), "days"),
                                    formatSpecifier: CheckFormatSpecifer(
                                        CheckNodeConstant(",d")
                                   )
                                )
                            )
                        )
                    )
                );

                errors.Errors.Should().BeEmpty();
            }
        }

        [TestMethod, Priority(0)]
        public void FStringEqualsErrors() {
            ParseErrors("FStringEqualsErrors.py",
                PythonLanguageVersion.V38,
                new ErrorResult("f-string: expecting '}' but found 'f'", new SourceSpan(1, 9, 1, 10)),
                new ErrorResult("f-string: expecting '}' but found 'a'", new SourceSpan(2, 10, 2, 11))
            );
        }

        [TestMethod, Priority(0)]
        public void GeneralizedUnpacking() {
            foreach (var version in V35AndUp) {
                CheckAst(
                    ParseFile("GenUnpack.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckCallStmt(Fob,
                            CheckNamedArg("*", CheckListExpr(One)),
                            CheckNamedArg("*", CheckListExpr(Two)),
                            PositionalArg(Three)
                        ),
                        CheckCallStmt(Fob,
                            CheckNamedArg("**", CheckDictionaryExpr(CheckSlice(CheckConstant("x"), One))),
                            CheckNamedArg("y", Two),
                            CheckNamedArg("**", CheckDictionaryExpr(CheckSlice(CheckConstant("z"), Three)))
                        ),
                        CheckTupleStmt(
                            CheckStarExpr(
                                CheckCallExpression(Fob, PositionalArg(Four))
                            ),
                            Four
                        ),
                        CheckExprStmt(
                            CheckListExpr(
                                CheckStarExpr(
                                    CheckCallExpression(Fob, PositionalArg(Four))
                                ),
                                Four
                            )
                        ),
                        CheckExprStmt(
                            CheckSetLiteral(
                                CheckStarExpr(CheckCallExpression(Fob, PositionalArg(Four))),
                                Four
                            )
                        ),
                        CheckDictionaryStmt(
                            CheckSlice(
                                CheckConstant("x"),
                                One
                            ),
                            CheckDictValueOnly(
                                CheckStarExpr(CheckDictionaryExpr(
                                    CheckSlice(CheckConstant("y"), Two)
                                ), 2)
                            )
                        ),
                        CheckDictionaryStmt(
                            CheckDictValueOnly(
                                CheckStarExpr(CheckDictionaryExpr(
                                    CheckSlice(CheckConstant("x"), Two)
                                ), 2)
                            ),
                            CheckSlice(
                                CheckConstant("x"),
                                One
                            )
                        ),
                        CheckExprStmt(
                            CheckSetLiteral(
                                CheckStarExpr(CheckListExpr())
                            )
                        )
                    )
                );
            }
        }

        [DataRow(100, 50, 1)]
        [DataRow(10, 3, 2)]
        [DataTestMethod, Priority(0)]
        public void Errors(int index, int line, int column) {
            foreach (var version in V30_V32Versions) {
                ParseErrors("Errors3x.py",
                    version,
                    new ErrorResult("no binding for nonlocal '__class__' found", new SourceSpan(2, 14, 2, 23))
                );
            }

            var initLoc = new SourceLocation(index, line, column);

            ParseErrorsWithOffset("AllErrors.py",
                PythonLanguageVersion.V24,
                initLoc,
                new ErrorResult("future statement does not support import *", new SourceSpan(1, 1, 1, 25)),
                new ErrorResult("future feature is not defined: *", new SourceSpan(1, 1, 1, 25)),
                new ErrorResult("not a chance", new SourceSpan(2, 1, 2, 30)),
                new ErrorResult("future feature is not defined: unknown", new SourceSpan(3, 1, 3, 31)),
                new ErrorResult("default value must be specified here", new SourceSpan(5, 16, 5, 17)),
                new ErrorResult("non-keyword arg after keyword arg", new SourceSpan(8, 12, 8, 13)),
                new ErrorResult("only one * allowed", new SourceSpan(9, 10, 9, 12)),
                new ErrorResult("only one ** allowed", new SourceSpan(10, 11, 10, 14)),
                new ErrorResult("keywords must come before ** args", new SourceSpan(11, 13, 11, 19)),
                new ErrorResult("unexpected token 'pass'", new SourceSpan(14, 1, 14, 5)),
                new ErrorResult("invalid sublist parameter", new SourceSpan(17, 11, 17, 13)),
                new ErrorResult("invalid parameter", new SourceSpan(20, 10, 20, 12)),
                new ErrorResult("'break' outside loop", new SourceSpan(25, 1, 25, 6)),
                new ErrorResult("'continue' not properly in loop", new SourceSpan(26, 1, 26, 9)),
                new ErrorResult("print statement expected expression to be printed", new SourceSpan(28, 1, 28, 15)),
                new ErrorResult("'continue' not supported inside 'finally' clause", new SourceSpan(34, 9, 34, 17)),
                new ErrorResult("expected expression after del", new SourceSpan(36, 1, 36, 4)),
                new ErrorResult("can't delete binary operator", new SourceSpan(37, 5, 37, 8)),
                new ErrorResult("can't delete unary operator", new SourceSpan(38, 5, 38, 7)),
                new ErrorResult("can't delete or expression", new SourceSpan(39, 5, 39, 13)),
                new ErrorResult("can't delete and expression", new SourceSpan(40, 5, 40, 14)),
                new ErrorResult("can't delete dictionary display", new SourceSpan(41, 5, 41, 7)),
                new ErrorResult("can't delete literal", new SourceSpan(42, 5, 42, 9)),
                new ErrorResult("can't delete literal", new SourceSpan(43, 5, 43, 9)),
                new ErrorResult("can't assign to literal", new SourceSpan(45, 1, 45, 5)),
                new ErrorResult("can't assign to literal", new SourceSpan(46, 1, 46, 5)),
                new ErrorResult("'return' outside function", new SourceSpan(48, 1, 48, 7)),
                new ErrorResult("'return' with argument inside generator", new SourceSpan(53, 5, 53, 14)),
                new ErrorResult("misplaced yield", new SourceSpan(55, 1, 55, 6)),
                new ErrorResult("'return' with argument inside generator", new SourceSpan(59, 5, 59, 14)),
                new ErrorResult("'return' with argument inside generator", new SourceSpan(60, 5, 60, 15)),
                new ErrorResult("invalid syntax", new SourceSpan(68, 5, 68, 6)),
                new ErrorResult("invalid syntax", new SourceSpan(68, 9, 68, 10)),
                new ErrorResult("illegal expression for augmented assignment", new SourceSpan(70, 1, 70, 3)),
                new ErrorResult("missing module name", new SourceSpan(72, 6, 72, 12)),
                new ErrorResult("from __future__ imports must occur at the beginning of the file", new SourceSpan(78, 1, 78, 32)),
                new ErrorResult("unexpected token 'blazzz'", new SourceSpan(82, 10, 82, 16)),
                new ErrorResult("invalid syntax, from cause not allowed in 2.x.", new SourceSpan(87, 11, 87, 19)),
                new ErrorResult("invalid syntax, class decorators require 2.6 or later.", new SourceSpan(93, 1, 93, 6)),
                new ErrorResult("invalid syntax, parameter annotations require 3.x", new SourceSpan(96, 7, 96, 12)),
                new ErrorResult("default value must be specified here", new SourceSpan(99, 15, 99, 16)),
                new ErrorResult("positional parameter after * args not allowed", new SourceSpan(102, 13, 102, 19)),
                new ErrorResult("duplicate * args arguments", new SourceSpan(105, 13, 105, 15)),
                new ErrorResult("duplicate * args arguments", new SourceSpan(108, 13, 108, 15)),
                new ErrorResult("invalid syntax", new SourceSpan(111, 10, 111, 11)),
                new ErrorResult("invalid sublist parameter", new SourceSpan(117, 11, 117, 13)),
                new ErrorResult("duplicate argument 'abc' in function definition", new SourceSpan(120, 12, 120, 15)),
                new ErrorResult("duplicate argument 'abc' in function definition", new SourceSpan(123, 16, 123, 19)),
                new ErrorResult("invalid parameter", new SourceSpan(127, 7, 127, 9)),
                new ErrorResult("default 'except' must be last", new SourceSpan(132, 1, 132, 8)),
                new ErrorResult("'as' requires Python 2.6 or later", new SourceSpan(139, 18, 139, 20)),
                new ErrorResult("invalid syntax", new SourceSpan(147, 2, 147, 7)),
                new ErrorResult("invalid syntax", new SourceSpan(147, 8, 147, 13)),
                new ErrorResult("unexpected token 'b'", new SourceSpan(148, 7, 148, 8)),
                new ErrorResult("invalid syntax", new SourceSpan(149, 7, 149, 9)),
                new ErrorResult("invalid syntax", new SourceSpan(149, 7, 149, 9)),
                new ErrorResult("invalid syntax", new SourceSpan(150, 2, 150, 7)),
                new ErrorResult("invalid syntax", new SourceSpan(150, 8, 150, 10)),
                new ErrorResult("invalid syntax", new SourceSpan(152, 4, 152, 6)),
                new ErrorResult("expected name", new SourceSpan(154, 3, 154, 5)),
                new ErrorResult("invalid parameter", new SourceSpan(156, 7, 156, 13)),
                new ErrorResult("invalid syntax, set literals require Python 2.7 or later.", new SourceSpan(160, 12, 160, 13)),
                new ErrorResult("invalid syntax, set literals require Python 2.7 or later.", new SourceSpan(161, 7, 161, 8))
            );

            ParseErrorsWithOffset("AllErrors.py",
                PythonLanguageVersion.V25,
                initLoc,
                new ErrorResult("future statement does not support import *", new SourceSpan(1, 1, 1, 25)),
                new ErrorResult("future feature is not defined: *", new SourceSpan(1, 1, 1, 25)),
                new ErrorResult("not a chance", new SourceSpan(2, 1, 2, 30)),
                new ErrorResult("future feature is not defined: unknown", new SourceSpan(3, 1, 3, 31)),
                new ErrorResult("default value must be specified here", new SourceSpan(5, 16, 5, 17)),
                new ErrorResult("non-keyword arg after keyword arg", new SourceSpan(8, 12, 8, 13)),
                new ErrorResult("only one * allowed", new SourceSpan(9, 10, 9, 12)),
                new ErrorResult("only one ** allowed", new SourceSpan(10, 11, 10, 14)),
                new ErrorResult("keywords must come before ** args", new SourceSpan(11, 13, 11, 19)),
                new ErrorResult("unexpected token 'pass'", new SourceSpan(14, 1, 14, 5)),
                new ErrorResult("invalid sublist parameter", new SourceSpan(17, 11, 17, 13)),
                new ErrorResult("invalid parameter", new SourceSpan(20, 10, 20, 12)),
                new ErrorResult("'break' outside loop", new SourceSpan(25, 1, 25, 6)),
                new ErrorResult("'continue' not properly in loop", new SourceSpan(26, 1, 26, 9)),
                new ErrorResult("print statement expected expression to be printed", new SourceSpan(28, 1, 28, 15)),
                new ErrorResult("'continue' not supported inside 'finally' clause", new SourceSpan(34, 9, 34, 17)),
                new ErrorResult("expected expression after del", new SourceSpan(36, 1, 36, 4)),
                new ErrorResult("can't delete binary operator", new SourceSpan(37, 5, 37, 8)),
                new ErrorResult("can't delete unary operator", new SourceSpan(38, 5, 38, 7)),
                new ErrorResult("can't delete or expression", new SourceSpan(39, 5, 39, 13)),
                new ErrorResult("can't delete and expression", new SourceSpan(40, 5, 40, 14)),
                new ErrorResult("can't delete dictionary display", new SourceSpan(41, 5, 41, 7)),
                new ErrorResult("can't delete literal", new SourceSpan(42, 5, 42, 9)),
                new ErrorResult("can't delete literal", new SourceSpan(43, 5, 43, 9)),
                new ErrorResult("can't assign to literal", new SourceSpan(45, 1, 45, 5)),
                new ErrorResult("can't assign to literal", new SourceSpan(46, 1, 46, 5)),
                new ErrorResult("'return' outside function", new SourceSpan(48, 1, 48, 7)),
                new ErrorResult("'return' with argument inside generator", new SourceSpan(53, 5, 53, 14)),
                new ErrorResult("misplaced yield", new SourceSpan(55, 1, 55, 6)),
                new ErrorResult("'return' with argument inside generator", new SourceSpan(59, 5, 59, 14)),
                new ErrorResult("'return' with argument inside generator", new SourceSpan(60, 5, 60, 15)),
                new ErrorResult("invalid syntax", new SourceSpan(68, 5, 68, 6)),
                new ErrorResult("invalid syntax", new SourceSpan(68, 9, 68, 10)),
                new ErrorResult("illegal expression for augmented assignment", new SourceSpan(70, 1, 70, 3)),
                new ErrorResult("missing module name", new SourceSpan(72, 6, 72, 12)),
                new ErrorResult("from __future__ imports must occur at the beginning of the file", new SourceSpan(78, 1, 78, 32)),
                new ErrorResult("unexpected token 'blazzz'", new SourceSpan(82, 10, 82, 16)),
                new ErrorResult("invalid syntax, from cause not allowed in 2.x.", new SourceSpan(87, 11, 87, 19)),
                new ErrorResult("invalid syntax, class decorators require 2.6 or later.", new SourceSpan(93, 1, 93, 6)),
                new ErrorResult("invalid syntax, parameter annotations require 3.x", new SourceSpan(96, 7, 96, 12)),
                new ErrorResult("default value must be specified here", new SourceSpan(99, 15, 99, 16)),
                new ErrorResult("positional parameter after * args not allowed", new SourceSpan(102, 13, 102, 19)),
                new ErrorResult("duplicate * args arguments", new SourceSpan(105, 13, 105, 15)),
                new ErrorResult("duplicate * args arguments", new SourceSpan(108, 13, 108, 15)),
                new ErrorResult("invalid syntax", new SourceSpan(111, 10, 111, 11)),
                new ErrorResult("invalid sublist parameter", new SourceSpan(117, 11, 117, 13)),
                new ErrorResult("duplicate argument 'abc' in function definition", new SourceSpan(120, 12, 120, 15)),
                new ErrorResult("duplicate argument 'abc' in function definition", new SourceSpan(123, 16, 123, 19)),
                new ErrorResult("invalid parameter", new SourceSpan(127, 7, 127, 9)),
                new ErrorResult("default 'except' must be last", new SourceSpan(132, 1, 132, 8)),
                new ErrorResult("'as' requires Python 2.6 or later", new SourceSpan(139, 18, 139, 20)),
                new ErrorResult("invalid syntax", new SourceSpan(147, 2, 147, 7)),
                new ErrorResult("invalid syntax", new SourceSpan(147, 8, 147, 13)),
                new ErrorResult("unexpected token 'b'", new SourceSpan(148, 7, 148, 8)),
                new ErrorResult("invalid syntax", new SourceSpan(149, 7, 149, 9)),
                new ErrorResult("invalid syntax", new SourceSpan(149, 7, 149, 9)),
                new ErrorResult("invalid syntax", new SourceSpan(150, 2, 150, 7)),
                new ErrorResult("invalid syntax", new SourceSpan(150, 8, 150, 10)),
                new ErrorResult("invalid syntax", new SourceSpan(152, 4, 152, 6)),
                new ErrorResult("expected name", new SourceSpan(154, 3, 154, 5)),
                new ErrorResult("invalid parameter", new SourceSpan(156, 7, 156, 13)),
                new ErrorResult("invalid syntax, set literals require Python 2.7 or later.", new SourceSpan(160, 12, 160, 13)),
                new ErrorResult("invalid syntax, set literals require Python 2.7 or later.", new SourceSpan(161, 7, 161, 8))
            );

            ParseErrorsWithOffset("AllErrors.py",
                PythonLanguageVersion.V26,
                initLoc,
                new ErrorResult("future statement does not support import *", new SourceSpan(1, 1, 1, 25)),
                new ErrorResult("future feature is not defined: *", new SourceSpan(1, 1, 1, 25)),
                new ErrorResult("not a chance", new SourceSpan(2, 1, 2, 30)),
                new ErrorResult("future feature is not defined: unknown", new SourceSpan(3, 1, 3, 31)),
                new ErrorResult("default value must be specified here", new SourceSpan(5, 16, 5, 17)),
                new ErrorResult("non-keyword arg after keyword arg", new SourceSpan(8, 12, 8, 13)),
                new ErrorResult("only one * allowed", new SourceSpan(9, 10, 9, 12)),
                new ErrorResult("only one ** allowed", new SourceSpan(10, 11, 10, 14)),
                new ErrorResult("keywords must come before ** args", new SourceSpan(11, 13, 11, 19)),
                new ErrorResult("unexpected token 'pass'", new SourceSpan(14, 1, 14, 5)),
                new ErrorResult("invalid sublist parameter", new SourceSpan(17, 11, 17, 13)),
                new ErrorResult("invalid parameter", new SourceSpan(20, 10, 20, 12)),
                new ErrorResult("'break' outside loop", new SourceSpan(25, 1, 25, 6)),
                new ErrorResult("'continue' not properly in loop", new SourceSpan(26, 1, 26, 9)),
                new ErrorResult("print statement expected expression to be printed", new SourceSpan(28, 1, 28, 15)),
                new ErrorResult("'continue' not supported inside 'finally' clause", new SourceSpan(34, 9, 34, 17)),
                new ErrorResult("expected expression after del", new SourceSpan(36, 1, 36, 4)),
                new ErrorResult("can't delete binary operator", new SourceSpan(37, 5, 37, 8)),
                new ErrorResult("can't delete unary operator", new SourceSpan(38, 5, 38, 7)),
                new ErrorResult("can't delete or expression", new SourceSpan(39, 5, 39, 13)),
                new ErrorResult("can't delete and expression", new SourceSpan(40, 5, 40, 14)),
                new ErrorResult("can't delete dictionary display", new SourceSpan(41, 5, 41, 7)),
                new ErrorResult("can't delete literal", new SourceSpan(42, 5, 42, 9)),
                new ErrorResult("can't delete literal", new SourceSpan(43, 5, 43, 9)),
                new ErrorResult("can't assign to literal", new SourceSpan(45, 1, 45, 5)),
                new ErrorResult("can't assign to literal", new SourceSpan(46, 1, 46, 5)),
                new ErrorResult("'return' outside function", new SourceSpan(48, 1, 48, 7)),
                new ErrorResult("'return' with argument inside generator", new SourceSpan(53, 5, 53, 14)),
                new ErrorResult("misplaced yield", new SourceSpan(55, 1, 55, 6)),
                new ErrorResult("'return' with argument inside generator", new SourceSpan(59, 5, 59, 14)),
                new ErrorResult("'return' with argument inside generator", new SourceSpan(60, 5, 60, 15)),
                new ErrorResult("invalid syntax", new SourceSpan(68, 5, 68, 6)),
                new ErrorResult("invalid syntax", new SourceSpan(68, 9, 68, 10)),
                new ErrorResult("illegal expression for augmented assignment", new SourceSpan(70, 1, 70, 3)),
                new ErrorResult("missing module name", new SourceSpan(72, 6, 72, 12)),
                new ErrorResult("from __future__ imports must occur at the beginning of the file", new SourceSpan(78, 1, 78, 32)),
                new ErrorResult("unexpected token 'blazzz'", new SourceSpan(82, 10, 82, 16)),
                new ErrorResult("invalid syntax, from cause not allowed in 2.x.", new SourceSpan(87, 11, 87, 19)),
                new ErrorResult("invalid syntax, parameter annotations require 3.x", new SourceSpan(96, 7, 96, 12)),
                new ErrorResult("default value must be specified here", new SourceSpan(99, 15, 99, 16)),
                new ErrorResult("positional parameter after * args not allowed", new SourceSpan(102, 13, 102, 19)),
                new ErrorResult("duplicate * args arguments", new SourceSpan(105, 13, 105, 15)),
                new ErrorResult("duplicate * args arguments", new SourceSpan(108, 13, 108, 15)),
                new ErrorResult("invalid syntax", new SourceSpan(111, 10, 111, 11)),
                new ErrorResult("invalid sublist parameter", new SourceSpan(117, 11, 117, 13)),
                new ErrorResult("duplicate argument 'abc' in function definition", new SourceSpan(120, 12, 120, 15)),
                new ErrorResult("duplicate argument 'abc' in function definition", new SourceSpan(123, 16, 123, 19)),
                new ErrorResult("invalid parameter", new SourceSpan(127, 7, 127, 9)),
                new ErrorResult("default 'except' must be last", new SourceSpan(132, 1, 132, 8)),
                new ErrorResult("invalid syntax", new SourceSpan(149, 7, 149, 9)),
                new ErrorResult("invalid syntax", new SourceSpan(149, 7, 149, 9)),
                new ErrorResult("invalid syntax", new SourceSpan(150, 8, 150, 10)),
                new ErrorResult("invalid syntax", new SourceSpan(150, 8, 150, 10)),
                new ErrorResult("invalid syntax", new SourceSpan(152, 4, 152, 6)),
                new ErrorResult("expected name", new SourceSpan(154, 3, 154, 5)),
                new ErrorResult("invalid parameter", new SourceSpan(156, 7, 156, 13)),
                new ErrorResult("invalid syntax, set literals require Python 2.7 or later.", new SourceSpan(160, 12, 160, 13)),
                new ErrorResult("invalid syntax, set literals require Python 2.7 or later.", new SourceSpan(161, 7, 161, 8))
            );

            ParseErrorsWithOffset("AllErrors.py",
                PythonLanguageVersion.V27,
                initLoc,
                new ErrorResult("future statement does not support import *", new SourceSpan(1, 1, 1, 25)),
                new ErrorResult("future feature is not defined: *", new SourceSpan(1, 1, 1, 25)),
                new ErrorResult("not a chance", new SourceSpan(2, 1, 2, 30)),
                new ErrorResult("future feature is not defined: unknown", new SourceSpan(3, 1, 3, 31)),
                new ErrorResult("default value must be specified here", new SourceSpan(5, 16, 5, 17)),
                new ErrorResult("non-keyword arg after keyword arg", new SourceSpan(8, 12, 8, 13)),
                new ErrorResult("only one * allowed", new SourceSpan(9, 10, 9, 12)),
                new ErrorResult("only one ** allowed", new SourceSpan(10, 11, 10, 14)),
                new ErrorResult("keywords must come before ** args", new SourceSpan(11, 13, 11, 19)),
                new ErrorResult("unexpected token 'pass'", new SourceSpan(14, 1, 14, 5)),
                new ErrorResult("invalid sublist parameter", new SourceSpan(17, 11, 17, 13)),
                new ErrorResult("invalid parameter", new SourceSpan(20, 10, 20, 12)),
                new ErrorResult("'break' outside loop", new SourceSpan(25, 1, 25, 6)),
                new ErrorResult("'continue' not properly in loop", new SourceSpan(26, 1, 26, 9)),
                new ErrorResult("print statement expected expression to be printed", new SourceSpan(28, 1, 28, 15)),
                new ErrorResult("'continue' not supported inside 'finally' clause", new SourceSpan(34, 9, 34, 17)),
                new ErrorResult("expected expression after del", new SourceSpan(36, 1, 36, 4)),
                new ErrorResult("can't delete binary operator", new SourceSpan(37, 5, 37, 8)),
                new ErrorResult("can't delete unary operator", new SourceSpan(38, 5, 38, 7)),
                new ErrorResult("can't delete or expression", new SourceSpan(39, 5, 39, 13)),
                new ErrorResult("can't delete and expression", new SourceSpan(40, 5, 40, 14)),
                new ErrorResult("can't delete dictionary display", new SourceSpan(41, 5, 41, 7)),
                new ErrorResult("can't delete literal", new SourceSpan(42, 5, 42, 9)),
                new ErrorResult("can't delete literal", new SourceSpan(43, 5, 43, 9)),
                new ErrorResult("can't assign to literal", new SourceSpan(45, 1, 45, 5)),
                new ErrorResult("can't assign to literal", new SourceSpan(46, 1, 46, 5)),
                new ErrorResult("'return' outside function", new SourceSpan(48, 1, 48, 7)),
                new ErrorResult("'return' with argument inside generator", new SourceSpan(53, 5, 53, 14)),
                new ErrorResult("misplaced yield", new SourceSpan(55, 1, 55, 6)),
                new ErrorResult("'return' with argument inside generator", new SourceSpan(59, 5, 59, 14)),
                new ErrorResult("'return' with argument inside generator", new SourceSpan(60, 5, 60, 15)),
                new ErrorResult("invalid syntax", new SourceSpan(68, 5, 68, 6)),
                new ErrorResult("invalid syntax", new SourceSpan(68, 9, 68, 10)),
                new ErrorResult("illegal expression for augmented assignment", new SourceSpan(70, 1, 70, 3)),
                new ErrorResult("missing module name", new SourceSpan(72, 6, 72, 12)),
                new ErrorResult("from __future__ imports must occur at the beginning of the file", new SourceSpan(78, 1, 78, 32)),
                new ErrorResult("unexpected token 'blazzz'", new SourceSpan(82, 10, 82, 16)),
                new ErrorResult("invalid syntax, from cause not allowed in 2.x.", new SourceSpan(87, 11, 87, 19)),
                new ErrorResult("invalid syntax, parameter annotations require 3.x", new SourceSpan(96, 7, 96, 12)),
                new ErrorResult("default value must be specified here", new SourceSpan(99, 15, 99, 16)),
                new ErrorResult("positional parameter after * args not allowed", new SourceSpan(102, 13, 102, 19)),
                new ErrorResult("duplicate * args arguments", new SourceSpan(105, 13, 105, 15)),
                new ErrorResult("duplicate * args arguments", new SourceSpan(108, 13, 108, 15)),
                new ErrorResult("invalid syntax", new SourceSpan(111, 10, 111, 11)),
                new ErrorResult("invalid sublist parameter", new SourceSpan(117, 11, 117, 13)),
                new ErrorResult("duplicate argument 'abc' in function definition", new SourceSpan(120, 12, 120, 15)),
                new ErrorResult("duplicate argument 'abc' in function definition", new SourceSpan(123, 16, 123, 19)),
                new ErrorResult("invalid parameter", new SourceSpan(127, 7, 127, 9)),
                new ErrorResult("default 'except' must be last", new SourceSpan(132, 1, 132, 8)),
                new ErrorResult("invalid syntax", new SourceSpan(149, 7, 149, 9)),
                new ErrorResult("invalid syntax", new SourceSpan(149, 7, 149, 9)),
                new ErrorResult("invalid syntax", new SourceSpan(150, 8, 150, 10)),
                new ErrorResult("invalid syntax", new SourceSpan(150, 8, 150, 10)),
                new ErrorResult("invalid syntax", new SourceSpan(152, 4, 152, 6)),
                new ErrorResult("expected name", new SourceSpan(154, 3, 154, 5)),
                new ErrorResult("invalid parameter", new SourceSpan(156, 7, 156, 13)),
                new ErrorResult("invalid syntax", new SourceSpan(160, 12, 160, 13)),
                new ErrorResult("invalid syntax", new SourceSpan(161, 10, 161, 13))
            );

            foreach (var version in V30_V32Versions) {
                ParseErrorsWithOffset("AllErrors.py",
                    version,
                    initLoc,
                    new ErrorResult("future statement does not support import *", new SourceSpan(1, 1, 1, 25)),
                    new ErrorResult("future feature is not defined: *", new SourceSpan(1, 1, 1, 25)),
                    new ErrorResult("not a chance", new SourceSpan(2, 1, 2, 30)),
                    new ErrorResult("future feature is not defined: unknown", new SourceSpan(3, 1, 3, 31)),
                    new ErrorResult("default value must be specified here", new SourceSpan(5, 16, 5, 17)),
                    new ErrorResult("non-keyword arg after keyword arg", new SourceSpan(8, 12, 8, 13)),
                    new ErrorResult("only one * allowed", new SourceSpan(9, 10, 9, 12)),
                    new ErrorResult("only one ** allowed", new SourceSpan(10, 11, 10, 14)),
                    new ErrorResult("keywords must come before ** args", new SourceSpan(11, 13, 11, 19)),
                    new ErrorResult("unexpected token 'pass'", new SourceSpan(14, 1, 14, 5)),
                    new ErrorResult("sublist parameters are not supported in 3.x", new SourceSpan(17, 10, 17, 17)),
                    new ErrorResult("invalid parameter", new SourceSpan(20, 10, 20, 12)),
                    new ErrorResult("'break' outside loop", new SourceSpan(25, 1, 25, 6)),
                    new ErrorResult("'continue' not properly in loop", new SourceSpan(26, 1, 26, 9)),
                    new ErrorResult("'continue' not supported inside 'finally' clause", new SourceSpan(34, 9, 34, 17)),
                    new ErrorResult("expected expression after del", new SourceSpan(36, 1, 36, 4)),
                    new ErrorResult("can't delete binary operator", new SourceSpan(37, 5, 37, 8)),
                    new ErrorResult("can't delete unary operator", new SourceSpan(38, 5, 38, 7)),
                    new ErrorResult("can't delete or expression", new SourceSpan(39, 5, 39, 13)),
                    new ErrorResult("can't delete and expression", new SourceSpan(40, 5, 40, 14)),
                    new ErrorResult("can't delete dictionary display", new SourceSpan(41, 5, 41, 7)),
                    new ErrorResult("can't delete literal", new SourceSpan(42, 5, 42, 9)),
                    new ErrorResult("can't delete literal", new SourceSpan(43, 5, 43, 9)),
                    new ErrorResult("can't assign to literal", new SourceSpan(45, 1, 45, 5)),
                    new ErrorResult("can't assign to literal", new SourceSpan(46, 1, 46, 5)),
                    new ErrorResult("'return' outside function", new SourceSpan(48, 1, 48, 7)),
                    new ErrorResult("'return' with argument inside generator", new SourceSpan(53, 5, 53, 14)),
                    new ErrorResult("misplaced yield", new SourceSpan(55, 1, 55, 6)),
                    new ErrorResult("'return' with argument inside generator", new SourceSpan(59, 5, 59, 14)),
                    new ErrorResult("'return' with argument inside generator", new SourceSpan(60, 5, 60, 15)),
                    new ErrorResult("two starred expressions in assignment", new SourceSpan(68, 8, 68, 10)),
                    new ErrorResult("illegal expression for augmented assignment", new SourceSpan(70, 1, 70, 3)),
                    new ErrorResult("missing module name", new SourceSpan(72, 6, 72, 12)),
                    new ErrorResult("import * only allowed at module level", new SourceSpan(75, 19, 75, 20)),
                    new ErrorResult("from __future__ imports must occur at the beginning of the file", new SourceSpan(78, 1, 78, 32)),
                    new ErrorResult("nonlocal declaration not allowed at module level", new SourceSpan(82, 1, 82, 9)),
                    new ErrorResult("invalid syntax, only exception value is allowed in 3.x.", new SourceSpan(83, 10, 83, 15)),
                    new ErrorResult("default value must be specified here", new SourceSpan(99, 15, 99, 16)),
                    new ErrorResult("duplicate * args arguments", new SourceSpan(105, 13, 105, 15)),
                    new ErrorResult("duplicate * args arguments", new SourceSpan(108, 13, 108, 15)),
                    new ErrorResult("named arguments must follow bare *", new SourceSpan(111, 10, 111, 11)),
                    new ErrorResult("sublist parameters are not supported in 3.x", new SourceSpan(114, 10, 114, 16)),
                    new ErrorResult("sublist parameters are not supported in 3.x", new SourceSpan(117, 10, 117, 17)),
                    new ErrorResult("duplicate argument 'abc' in function definition", new SourceSpan(120, 12, 120, 15)),
                    new ErrorResult("sublist parameters are not supported in 3.x", new SourceSpan(123, 10, 123, 20)),
                    new ErrorResult("invalid parameter", new SourceSpan(127, 7, 127, 9)),
                    new ErrorResult("\", variable\" not allowed in 3.x - use \"as variable\" instead.", new SourceSpan(134, 17, 134, 20)),
                    new ErrorResult("default 'except' must be last", new SourceSpan(132, 1, 132, 8)),
                    new ErrorResult("\", variable\" not allowed in 3.x - use \"as variable\" instead.", new SourceSpan(144, 17, 144, 20)),
                    new ErrorResult("cannot mix bytes and nonbytes literals", new SourceSpan(147, 8, 147, 13)),
                    new ErrorResult("cannot mix bytes and nonbytes literals", new SourceSpan(148, 7, 148, 13)),
                    new ErrorResult("invalid syntax", new SourceSpan(149, 7, 149, 9)),
                    new ErrorResult("invalid syntax", new SourceSpan(149, 7, 149, 9)),
                    new ErrorResult("invalid syntax", new SourceSpan(150, 8, 150, 10)),
                    new ErrorResult("invalid syntax", new SourceSpan(150, 8, 150, 10)),
                    new ErrorResult("invalid syntax", new SourceSpan(152, 4, 152, 6)),
                    new ErrorResult("expected name", new SourceSpan(154, 3, 154, 5)),
                    new ErrorResult("invalid parameter", new SourceSpan(156, 7, 156, 13)),
                    new ErrorResult("invalid syntax", new SourceSpan(160, 12, 160, 13)),
                    new ErrorResult("invalid syntax", new SourceSpan(161, 10, 161, 13))
                );
            }

            foreach (var version in V33AndV34) {
                ParseErrorsWithOffset("AllErrors.py",
                    version,
                    initLoc,
                    new ErrorResult("future statement does not support import *", new SourceSpan(1, 1, 1, 25)),
                    new ErrorResult("future feature is not defined: *", new SourceSpan(1, 1, 1, 25)),
                    new ErrorResult("not a chance", new SourceSpan(2, 1, 2, 30)),
                    new ErrorResult("future feature is not defined: unknown", new SourceSpan(3, 1, 3, 31)),
                    new ErrorResult("default value must be specified here", new SourceSpan(5, 16, 5, 17)),
                    new ErrorResult("non-keyword arg after keyword arg", new SourceSpan(8, 12, 8, 13)),
                    new ErrorResult("only one * allowed", new SourceSpan(9, 10, 9, 12)),
                    new ErrorResult("only one ** allowed", new SourceSpan(10, 11, 10, 14)),
                    new ErrorResult("keywords must come before ** args", new SourceSpan(11, 13, 11, 19)),
                    new ErrorResult("unexpected token 'pass'", new SourceSpan(14, 1, 14, 5)),
                    new ErrorResult("sublist parameters are not supported in 3.x", new SourceSpan(17, 10, 17, 17)),
                    new ErrorResult("invalid parameter", new SourceSpan(20, 10, 20, 12)),
                    new ErrorResult("'break' outside loop", new SourceSpan(25, 1, 25, 6)),
                    new ErrorResult("'continue' not properly in loop", new SourceSpan(26, 1, 26, 9)),
                    new ErrorResult("'continue' not supported inside 'finally' clause", new SourceSpan(34, 9, 34, 17)),
                    new ErrorResult("expected expression after del", new SourceSpan(36, 1, 36, 4)),
                    new ErrorResult("can't delete binary operator", new SourceSpan(37, 5, 37, 8)),
                    new ErrorResult("can't delete unary operator", new SourceSpan(38, 5, 38, 7)),
                    new ErrorResult("can't delete or expression", new SourceSpan(39, 5, 39, 13)),
                    new ErrorResult("can't delete and expression", new SourceSpan(40, 5, 40, 14)),
                    new ErrorResult("can't delete dictionary display", new SourceSpan(41, 5, 41, 7)),
                    new ErrorResult("can't delete literal", new SourceSpan(42, 5, 42, 9)),
                    new ErrorResult("can't delete literal", new SourceSpan(43, 5, 43, 9)),
                    new ErrorResult("can't assign to literal", new SourceSpan(45, 1, 45, 5)),
                    new ErrorResult("can't assign to literal", new SourceSpan(46, 1, 46, 5)),
                    new ErrorResult("'return' outside function", new SourceSpan(48, 1, 48, 7)),
                    new ErrorResult("misplaced yield", new SourceSpan(55, 1, 55, 6)),
                    new ErrorResult("two starred expressions in assignment", new SourceSpan(68, 8, 68, 10)),
                    new ErrorResult("illegal expression for augmented assignment", new SourceSpan(70, 1, 70, 3)),
                    new ErrorResult("missing module name", new SourceSpan(72, 6, 72, 12)),
                    new ErrorResult("import * only allowed at module level", new SourceSpan(75, 19, 75, 20)),
                    new ErrorResult("from __future__ imports must occur at the beginning of the file", new SourceSpan(78, 1, 78, 32)),
                    new ErrorResult("nonlocal declaration not allowed at module level", new SourceSpan(82, 1, 82, 9)),
                    new ErrorResult("invalid syntax, only exception value is allowed in 3.x.", new SourceSpan(83, 10, 83, 15)),
                    new ErrorResult("default value must be specified here", new SourceSpan(99, 15, 99, 16)),
                    new ErrorResult("duplicate * args arguments", new SourceSpan(105, 13, 105, 15)),
                    new ErrorResult("duplicate * args arguments", new SourceSpan(108, 13, 108, 15)),
                    new ErrorResult("named arguments must follow bare *", new SourceSpan(111, 10, 111, 11)),
                    new ErrorResult("sublist parameters are not supported in 3.x", new SourceSpan(114, 10, 114, 16)),
                    new ErrorResult("sublist parameters are not supported in 3.x", new SourceSpan(117, 10, 117, 17)),
                    new ErrorResult("duplicate argument 'abc' in function definition", new SourceSpan(120, 12, 120, 15)),
                    new ErrorResult("sublist parameters are not supported in 3.x", new SourceSpan(123, 10, 123, 20)),
                    new ErrorResult("invalid parameter", new SourceSpan(127, 7, 127, 9)),
                    new ErrorResult("\", variable\" not allowed in 3.x - use \"as variable\" instead.", new SourceSpan(134, 17, 134, 20)),
                    new ErrorResult("default 'except' must be last", new SourceSpan(132, 1, 132, 8)),
                    new ErrorResult("\", variable\" not allowed in 3.x - use \"as variable\" instead.", new SourceSpan(144, 17, 144, 20)),
                    new ErrorResult("cannot mix bytes and nonbytes literals", new SourceSpan(147, 8, 147, 13)),
                    new ErrorResult("cannot mix bytes and nonbytes literals", new SourceSpan(148, 7, 148, 13)),
                    new ErrorResult("invalid syntax", new SourceSpan(149, 7, 149, 9)),
                    new ErrorResult("invalid syntax", new SourceSpan(149, 7, 149, 9)),
                    new ErrorResult("invalid syntax", new SourceSpan(150, 8, 150, 10)),
                    new ErrorResult("invalid syntax", new SourceSpan(150, 8, 150, 10)),
                    new ErrorResult("invalid syntax", new SourceSpan(152, 4, 152, 6)),
                    new ErrorResult("expected name", new SourceSpan(154, 3, 154, 5)),
                    new ErrorResult("invalid parameter", new SourceSpan(156, 7, 156, 13)),
                    new ErrorResult("invalid syntax", new SourceSpan(160, 12, 160, 13)),
                    new ErrorResult("invalid syntax", new SourceSpan(161, 10, 161, 13))
                );
            }

            foreach (var version in V35AndUp) {
                ParseErrorsWithOffset("AllErrors.py",
                    version,
                    initLoc,
                    new ErrorResult("future statement does not support import *", new SourceSpan(1, 1, 1, 25)),
                    new ErrorResult("future feature is not defined: *", new SourceSpan(1, 1, 1, 25)),
                    new ErrorResult("not a chance", new SourceSpan(2, 1, 2, 30)),
                    new ErrorResult("future feature is not defined: unknown", new SourceSpan(3, 1, 3, 31)),
                    new ErrorResult("default value must be specified here", new SourceSpan(5, 16, 5, 17)),
                    new ErrorResult("positional argument follows keyword argument", new SourceSpan(8, 12, 8, 13)),
                    new ErrorResult("unexpected token 'pass'", new SourceSpan(14, 1, 14, 5)),
                    new ErrorResult("sublist parameters are not supported in 3.x", new SourceSpan(17, 10, 17, 17)),
                    new ErrorResult("invalid parameter", new SourceSpan(20, 10, 20, 12)),
                    new ErrorResult("'break' outside loop", new SourceSpan(25, 1, 25, 6)),
                    new ErrorResult("'continue' not properly in loop", new SourceSpan(26, 1, 26, 9)),
                    new ErrorResult("'continue' not supported inside 'finally' clause", new SourceSpan(34, 9, 34, 17)),
                    new ErrorResult("expected expression after del", new SourceSpan(36, 1, 36, 4)),
                    new ErrorResult("can't delete binary operator", new SourceSpan(37, 5, 37, 8)),
                    new ErrorResult("can't delete unary operator", new SourceSpan(38, 5, 38, 7)),
                    new ErrorResult("can't delete or expression", new SourceSpan(39, 5, 39, 13)),
                    new ErrorResult("can't delete and expression", new SourceSpan(40, 5, 40, 14)),
                    new ErrorResult("can't delete dictionary display", new SourceSpan(41, 5, 41, 7)),
                    new ErrorResult("can't delete literal", new SourceSpan(42, 5, 42, 9)),
                    new ErrorResult("can't delete literal", new SourceSpan(43, 5, 43, 9)),
                    new ErrorResult("can't assign to literal", new SourceSpan(45, 1, 45, 5)),
                    new ErrorResult("can't assign to literal", new SourceSpan(46, 1, 46, 5)),
                    new ErrorResult("'return' outside function", new SourceSpan(48, 1, 48, 7)),
                    new ErrorResult("misplaced yield", new SourceSpan(55, 1, 55, 6)),
                    new ErrorResult("two starred expressions in assignment", new SourceSpan(68, 8, 68, 10)),
                    new ErrorResult("illegal expression for augmented assignment", new SourceSpan(70, 1, 70, 3)),
                    new ErrorResult("missing module name", new SourceSpan(72, 6, 72, 12)),
                    new ErrorResult("import * only allowed at module level", new SourceSpan(75, 19, 75, 20)),
                    new ErrorResult("from __future__ imports must occur at the beginning of the file", new SourceSpan(78, 1, 78, 32)),
                    new ErrorResult("nonlocal declaration not allowed at module level", new SourceSpan(82, 1, 82, 9)),
                    new ErrorResult("invalid syntax, only exception value is allowed in 3.x.", new SourceSpan(83, 10, 83, 15)),
                    new ErrorResult("default value must be specified here", new SourceSpan(99, 15, 99, 16)),
                    new ErrorResult("duplicate * args arguments", new SourceSpan(105, 13, 105, 15)),
                    new ErrorResult("duplicate * args arguments", new SourceSpan(108, 13, 108, 15)),
                    new ErrorResult("named arguments must follow bare *", new SourceSpan(111, 10, 111, 11)),
                    new ErrorResult("sublist parameters are not supported in 3.x", new SourceSpan(114, 10, 114, 16)),
                    new ErrorResult("sublist parameters are not supported in 3.x", new SourceSpan(117, 10, 117, 17)),
                    new ErrorResult("duplicate argument 'abc' in function definition", new SourceSpan(120, 12, 120, 15)),
                    new ErrorResult("sublist parameters are not supported in 3.x", new SourceSpan(123, 10, 123, 20)),
                    new ErrorResult("invalid parameter", new SourceSpan(127, 7, 127, 9)),
                    new ErrorResult("\", variable\" not allowed in 3.x - use \"as variable\" instead.", new SourceSpan(134, 17, 134, 20)),
                    new ErrorResult("default 'except' must be last", new SourceSpan(132, 1, 132, 8)),
                    new ErrorResult("\", variable\" not allowed in 3.x - use \"as variable\" instead.", new SourceSpan(144, 17, 144, 20)),
                    new ErrorResult("cannot mix bytes and nonbytes literals", new SourceSpan(147, 8, 147, 13)),
                    new ErrorResult("cannot mix bytes and nonbytes literals", new SourceSpan(148, 7, 148, 13)),
                    new ErrorResult("invalid syntax", new SourceSpan(149, 7, 149, 9)),
                    new ErrorResult("invalid syntax", new SourceSpan(149, 7, 149, 9)),
                    new ErrorResult("invalid syntax", new SourceSpan(150, 8, 150, 10)),
                    new ErrorResult("invalid syntax", new SourceSpan(150, 8, 150, 10)),
                    new ErrorResult("invalid syntax", new SourceSpan(152, 4, 152, 6)),
                    new ErrorResult("expected name", new SourceSpan(154, 3, 154, 5)),
                    new ErrorResult("invalid parameter", new SourceSpan(156, 7, 156, 13)),
                    new ErrorResult("invalid syntax", new SourceSpan(160, 12, 160, 13)),
                    new ErrorResult("invalid syntax", new SourceSpan(161, 10, 161, 13))
                );
            }
        }

        private void ParseErrorsWithOffset(string filename, PythonLanguageVersion version, SourceLocation initialLocation,
            params ErrorResult[] errors) {
            ParseErrors(filename, version, new ParserOptions() {
                IndentationInconsistencySeverity = Severity.Hint,
                InitialSourceLocation = initialLocation
            }, errors.Select(e => AddOffset(initialLocation, e)).ToArray());
        }

        private ErrorResult AddOffset(SourceLocation initLoc, ErrorResult error) {
            var span = error.Span;
            return new ErrorResult(
                error.Message,
                new SourceSpan(
                span.Start.Line + initLoc.Line - 1,
                span.Start.Line == 1 ? span.Start.Column + initLoc.Column - 1 : span.Start.Column,
                span.End.Line + initLoc.Line - 1,
                span.End.Line == 1 ? span.End.Column + initLoc.Column - 1 : span.End.Column
                )
            );
        }

        [TestMethod, Priority(0)]
        public void InvalidUnicodeLiteral() {
            var position = 42 + Environment.NewLine.Length;
            foreach (var version in V26AndUp) {
                ParseErrors("InvalidUnicodeLiteral26Up.py",
                    version,
                    new ErrorResult($"'unicodeescape' codec can't decode bytes in position {position}: truncated \\uXXXX escape",
                       new SourceSpan(2, 1, 2, 9))
                );
            }

            foreach (var version in V2Versions) {
                ParseErrors("InvalidUnicodeLiteral2x.py",
                    version,
                    new ErrorResult("'unicodeescape' codec can't decode bytes in position 4: truncated \\uXXXX escape", new SourceSpan(1, 1, 1, 10))
                );
            }

            foreach (var version in V3Versions) {
                ParseErrors("InvalidUnicodeLiteral.py",
                    version,
                    new ErrorResult("'unicodeescape' codec can't decode bytes in position 3: truncated \\uXXXX escape", new SourceSpan(1, 1, 1, 9))
                );
            }
        }


        [TestMethod, Priority(0)]
        public void DedentError() {
            foreach (var version in AllVersions) {
                ParseErrors("DedentError.py",
                    version,
                    new ErrorResult("unindent does not match any outer indentation level", new SourceSpan(4, 1, 4, 6))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void DedentErrorLargeFile() {
            foreach (var version in AllVersions) {
                ParseErrors("DedentErrorLargeFile.py",
                    version,
                    new ErrorResult("unindent does not match any outer indentation level", new SourceSpan(10, 1, 10, 7))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void Literals() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("Literals.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckStrOrBytesStmt(version, "abc"),
                        CheckStrOrBytesStmt(version, "raw string"),
                        CheckStrOrBytesStmt(version, "raw string"),
                        CheckStrOrBytesStmt(version, "abc"),
                        CheckStrOrBytesStmt(version, "raw string"),
                        CheckStrOrBytesStmt(version, "raw string"),
                        CheckStrOrBytesStmt(version, "abc"),
                        CheckStrOrBytesStmt(version, "raw string"),
                        CheckStrOrBytesStmt(version, "raw string"),
                        CheckStrOrBytesStmt(version, "abc"),
                        CheckStrOrBytesStmt(version, "raw string"),
                        CheckStrOrBytesStmt(version, "raw string"),
                        CheckConstantStmtAndRepr(1000, "1000", version),
                        CheckConstantStmtAndRepr(2147483647, "2147483647", version),
                        CheckConstantStmtAndRepr(3.14, "3.14", version),
                        CheckConstantStmtAndRepr(10.0, "10.0", version),
                        CheckConstantStmtAndRepr(.001, "0.001", version),
                        CheckConstantStmtAndRepr(1e100, "1e+100", version),
                        CheckConstantStmtAndRepr(3.14e-10, "3.14e-10", version),
                        CheckConstantStmtAndRepr(0e0, "0.0", version),
                        CheckConstantStmtAndRepr(new Complex(0, 3.14), "3.14j", version),
                        CheckConstantStmtAndRepr(new Complex(0, 10), "10j", version),
                        CheckConstantStmt(new Complex(0, 10)),
                        CheckConstantStmtAndRepr(new Complex(0, .001), "0.001j", version),
                        CheckConstantStmtAndRepr(new Complex(0, 1e100), "1e+100j", version),
                        CheckConstantStmtAndRepr(new Complex(0, 3.14e-10), "3.14e-10j", version),
                        CheckConstantStmtAndRepr(-2147483648, "-2147483648", version),
                        CheckUnaryStmt(PythonOperator.Negate, CheckConstant(100))
                    )
                );
            }

            foreach (var version in V2Versions) {
                CheckAst(
                    ParseFile("LiteralsV2.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckConstantStmtAndRepr((BigInteger)1000, "1000L", version),
                        CheckConstantStmtAndRepr("unicode string", "u'unicode string'", version),
                        CheckConstantStmt("unicode string"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("unicode string"),
                        CheckConstantStmt("unicode string"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("unicode string"),
                        CheckConstantStmt("unicode string"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("unicode string"),
                        CheckConstantStmt("unicode string"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmtAndRepr("\\\'\"\a\b\f\n\r\t\u2026\v\x2A\x2A", "u'\\\\\\\'\"\\x07\\x08\\x0c\\n\\r\\t\\u2026\\x0b**'", PythonLanguageVersion.V27),
                        IgnoreStmt(), // u'\N{COLON}',
                        CheckUnaryStmt(PythonOperator.Negate, CheckConstant(new BigInteger(2147483648))),
                        CheckUnaryStmt(PythonOperator.Negate, CheckConstant(new BigInteger(2147483648))),
                        CheckConstantStmt(464),
                        CheckUnaryStmt(PythonOperator.Negate, CheckConstant(new BigInteger(100))),
                        CheckConstantStmt(new BigInteger(464)),
                        CheckConstantStmt(new BigInteger(5))
                    )
                );
            }

            foreach (var version in V30_V32Versions) {
                ParseErrors("LiteralsV2.py",
                    version,
                    new ErrorResult("invalid token", new SourceSpan(1, 5, 1, 6)),
                    new ErrorResult("invalid syntax", new SourceSpan(2, 2, 2, 18)),
                    new ErrorResult("invalid syntax", new SourceSpan(3, 2, 3, 18)),
                    new ErrorResult("invalid syntax", new SourceSpan(4, 3, 4, 16)),
                    new ErrorResult("invalid syntax", new SourceSpan(5, 3, 5, 16)),
                    new ErrorResult("invalid syntax", new SourceSpan(6, 3, 6, 16)),
                    new ErrorResult("invalid syntax", new SourceSpan(7, 3, 7, 16)),
                    new ErrorResult("invalid syntax", new SourceSpan(8, 2, 8, 22)),
                    new ErrorResult("invalid syntax", new SourceSpan(9, 2, 9, 22)),
                    new ErrorResult("invalid syntax", new SourceSpan(10, 3, 10, 20)),
                    new ErrorResult("invalid syntax", new SourceSpan(11, 3, 11, 20)),
                    new ErrorResult("invalid syntax", new SourceSpan(12, 3, 12, 20)),
                    new ErrorResult("invalid syntax", new SourceSpan(13, 3, 13, 20)),
                    new ErrorResult("invalid syntax", new SourceSpan(14, 2, 14, 18)),
                    new ErrorResult("invalid syntax", new SourceSpan(15, 2, 15, 18)),
                    new ErrorResult("invalid syntax", new SourceSpan(16, 3, 16, 16)),
                    new ErrorResult("invalid syntax", new SourceSpan(17, 3, 17, 16)),
                    new ErrorResult("invalid syntax", new SourceSpan(18, 3, 18, 16)),
                    new ErrorResult("invalid syntax", new SourceSpan(19, 3, 19, 16)),
                    new ErrorResult("invalid syntax", new SourceSpan(20, 2, 20, 22)),
                    new ErrorResult("invalid syntax", new SourceSpan(21, 2, 21, 22)),
                    new ErrorResult("invalid syntax", new SourceSpan(22, 3, 22, 20)),
                    new ErrorResult("invalid syntax", new SourceSpan(23, 3, 23, 20)),
                    new ErrorResult("invalid syntax", new SourceSpan(24, 3, 24, 20)),
                    new ErrorResult("invalid syntax", new SourceSpan(25, 3, 25, 20)),
                    new ErrorResult("invalid syntax", new SourceSpan(26, 2, 27, 36)),
                    new ErrorResult("invalid syntax", new SourceSpan(28, 2, 28, 13)),
                    new ErrorResult("invalid token", new SourceSpan(29, 12, 29, 13)),
                    new ErrorResult("invalid token", new SourceSpan(30, 12, 30, 13)),
                    new ErrorResult("invalid token", new SourceSpan(31, 1, 31, 5)),
                    new ErrorResult("invalid token", new SourceSpan(32, 5, 32, 6)),
                    new ErrorResult("invalid token", new SourceSpan(33, 6, 33, 7)),
                    new ErrorResult("invalid token", new SourceSpan(34, 7, 34, 8))
                );
            }

            foreach (var version in V33AndUp) {
                ParseErrors("LiteralsV2.py",
                    version,
                    new ErrorResult("invalid token", new SourceSpan(1, 5, 1, 6)),
                    new ErrorResult("invalid token", new SourceSpan(29, 12, 29, 13)),
                    new ErrorResult("invalid token", new SourceSpan(30, 12, 30, 13)),
                    new ErrorResult("invalid token", new SourceSpan(31, 1, 31, 5)),
                    new ErrorResult("invalid token", new SourceSpan(32, 5, 32, 6)),
                    new ErrorResult("invalid token", new SourceSpan(33, 6, 33, 7)),
                    new ErrorResult("invalid token", new SourceSpan(34, 7, 34, 8))
                );
                CheckAst(
                    ParseFile("LiteralsV3.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckConstantStmtAndRepr(true, "True", version),
                        CheckConstantStmtAndRepr(false, "False", version),
                        CheckConstantStmtAndRepr(new BigInteger(111222333444), "111222333444", version),
                        CheckConstantStmt("unicode string"),
                        CheckConstantStmt("unicode string"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("unicode string"),
                        CheckConstantStmt("unicode string"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("unicode string"),
                        CheckConstantStmt("unicode string"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("unicode string"),
                        CheckConstantStmt("unicode string"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmt("raw unicode"),
                        CheckConstantStmtAndRepr("\\\'\"\a\b\f\n\r\t\u2026\v\x2A\x2A", "'\\\\\\'\"\\x07\\x08\\x0c\\n\\r\\t\\u2026\\x0b**'", PythonLanguageVersion.V33),
                        IgnoreStmt()  // u'\N{COLON}'
                    )
                );
            }
        }

        [TestMethod, Priority(0)]
        public void Literals26() {
            foreach (var version in V26AndUp) {
                CheckAst(
                    ParseFile("Literals26.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckConstantStmt(464),
                        CheckConstantStmt(4)
                    )
                );
            }

            foreach (var version in V24_V25Versions) {
                ParseErrors("Literals26.py",
                    version,
                    new ErrorResult("unexpected token 'o720'", new SourceSpan(1, 2, 1, 6)),
                    new ErrorResult("unexpected token 'b100'", new SourceSpan(2, 2, 2, 6))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void Literals36() {
            foreach (var version in V36AndUp) {
                CheckAst(
                    ParseFile("Literals36.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckConstantStmt(10000000.0),
                        CheckConstantStmt(new BigInteger(0xCAFE_F00D)),
                        CheckConstantStmt(0b0011_1111_0100_1110),
                        CheckConstantStmt(0b1111_0000),
                        CheckExprStmt(CheckNameExpr("_1")),
                        CheckConstantStmt(10),
                        CheckConstantStmt(100.0),
                        CheckConstantStmt(100.0),   // Also reports an error
                        CheckConstantStmt(10.0),    // Also reports an error
                        CheckConstantStmt(3.14),    // Also reports an error
                        CheckConstantStmt(3.14),    // Also reports an error
                        CheckConstantStmt(3.14),
                        CheckConstantStmt(3.14),    // Also reports an error
                        CheckConstantStmt(new BigInteger(0xCAFE_F00D)),
                        CheckConstantStmt(new BigInteger(0xCAFE_F00D)),     // Error
                        CheckConstantStmt(511),
                        CheckConstantStmt(511),
                        CheckConstantStmt(511),
                        CheckConstantStmt(511)      // Also reports an error
                    )
                );

                ParseErrors("Literals36.py", version,
                    new ErrorResult("invalid token", new SourceSpan(8, 1, 8, 7)),
                    new ErrorResult("invalid token", new SourceSpan(9, 1, 9, 5)),
                    new ErrorResult("invalid token", new SourceSpan(10, 1, 10, 6)),
                    new ErrorResult("invalid token", new SourceSpan(11, 1, 11, 6)),
                    new ErrorResult("invalid token", new SourceSpan(13, 1, 13, 6)),
                    new ErrorResult("invalid token", new SourceSpan(15, 1, 15, 13)),
                    new ErrorResult("invalid token", new SourceSpan(19, 1, 19, 7))
                );
            }

            foreach (var version in AllVersions.Where(v => v <= PythonLanguageVersion.V35)) {
                ParseErrors("Literals36.py", version,
                    new ErrorResult("unexpected token '_000_000'", new SourceSpan(1, 3, 1, 11)),
                    new ErrorResult("unexpected token '_F00D'", new SourceSpan(2, 7, 2, 12)),
                    new ErrorResult("unexpected token '_0011_1111_0100_1110'", new SourceSpan(3, 3, 3, 23)),
                    new ErrorResult("unexpected token '_1111_0000'", new SourceSpan(4, 3, 4, 13)),
                    new ErrorResult("unexpected token '_0'", new SourceSpan(6, 2, 6, 4)),
                    new ErrorResult("unexpected token '_0e0_1'", new SourceSpan(7, 2, 7, 8)),
                    new ErrorResult("unexpected token '_0e_1'", new SourceSpan(8, 2, 8, 7)),
                    new ErrorResult("unexpected token '_e1'", new SourceSpan(9, 2, 9, 5)),
                    new ErrorResult("unexpected token '_'", new SourceSpan(10, 2, 10, 3)),
                    new ErrorResult("unexpected token '_14'", new SourceSpan(11, 3, 11, 6)),
                    new ErrorResult("unexpected token '_4'", new SourceSpan(12, 4, 12, 6)),
                    new ErrorResult("unexpected token '_'", new SourceSpan(13, 5, 13, 6)),
                    new ErrorResult("unexpected token '_CAFE_F00D'", new SourceSpan(14, 3, 14, 13)),
                    new ErrorResult("unexpected token '_F00D_'", new SourceSpan(15, 7, 15, 13)),
                    new ErrorResult("unexpected token '_777'", new SourceSpan(16, 3, 16, 7)),
                    new ErrorResult("unexpected token '_77'", new SourceSpan(17, 4, 17, 7)),
                    new ErrorResult("unexpected token '_7'", new SourceSpan(18, 5, 18, 7)),
                    new ErrorResult("unexpected token '_'", new SourceSpan(19, 6, 19, 7))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void Keywords25() {
            foreach (var version in V24_V25Versions) {
                CheckAst(
                    ParseFile("Keywords25.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckAssignment(CheckNameExpr("with"), One),
                        CheckAssignment(CheckNameExpr("as"), Two)
                    )
                );
            }

            foreach (var version in V26AndUp) {
                ParseErrors("Keywords25.py",
                    version,
                    new ErrorResult("unexpected token '='", new SourceSpan(1, 6, 1, 7)),
                    new ErrorResult("invalid syntax", new SourceSpan(1, 8, 1, 9)),
                    new ErrorResult("unexpected token '<newline>'", new SourceSpan(1, 9, 2, 1)),
                    new ErrorResult("unexpected token 'as'", new SourceSpan(2, 1, 2, 3)),
                    new ErrorResult("can't assign to error expression", new SourceSpan(2, 1, 2, 3))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void Keywords2x() {
            foreach (var version in V2Versions) {
                CheckAst(
                    ParseFile("Keywords2x.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckAssignment(CheckNameExpr("True"), One),
                        CheckAssignment(CheckNameExpr("False"), Zero)
                    )
                );
            }

            foreach (var version in V3Versions) {
                ParseErrors("Keywords2x.py",
                    version,
                    new ErrorResult("can't assign to literal", new SourceSpan(1, 1, 1, 5)),
                    new ErrorResult("can't assign to literal", new SourceSpan(2, 1, 2, 6))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void Keywords30() {
            foreach (var version in V3Versions) {
                CheckAst(
                    ParseFile("Keywords30.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckAssignment(Fob, CheckConstant(true)),
                        CheckAssignment(Oar, CheckConstant(false))
                    )
                );
            }

            foreach (var version in V2Versions) {
                CheckAst(
                     ParseFile("Keywords30.py", ErrorSink.Null, version),
                     CheckSuite(
                         CheckAssignment(Fob, CheckNameExpr("True")),
                         CheckAssignment(Oar, CheckNameExpr("False"))
                     )
                 );
            }
        }

        [TestMethod, Priority(0)]
        public void BinaryOperators() {
            foreach (var version in AllVersions) {

                CheckAst(
                    ParseFile("BinaryOperators.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckBinaryStmt(One, PythonOperator.Add, Two),
                        CheckBinaryStmt(One, PythonOperator.Subtract, Two),
                        CheckBinaryStmt(One, PythonOperator.Multiply, Two),
                        CheckBinaryStmt(One, PythonOperator.Power, Two),
                        CheckBinaryStmt(One, version.Is3x() ? PythonOperator.TrueDivide : PythonOperator.Divide, Two),
                        CheckBinaryStmt(One, PythonOperator.FloorDivide, Two),
                        CheckBinaryStmt(One, PythonOperator.Mod, Two),
                        CheckBinaryStmt(One, PythonOperator.LeftShift, Two),
                        CheckBinaryStmt(One, PythonOperator.RightShift, Two),
                        CheckBinaryStmt(One, PythonOperator.BitwiseAnd, Two),
                        CheckBinaryStmt(One, PythonOperator.BitwiseOr, Two),
                        CheckBinaryStmt(One, PythonOperator.Xor, Two),
                        CheckBinaryStmt(One, PythonOperator.LessThan, Two),
                        CheckBinaryStmt(One, PythonOperator.GreaterThan, Two),
                        CheckBinaryStmt(One, PythonOperator.LessThanOrEqual, Two),
                        CheckBinaryStmt(One, PythonOperator.GreaterThanOrEqual, Two),
                        CheckBinaryStmt(One, PythonOperator.Equal, Two),
                        CheckBinaryStmt(One, PythonOperator.NotEqual, Two),
                        CheckBinaryStmt(One, PythonOperator.Is, Two),
                        CheckBinaryStmt(One, PythonOperator.IsNot, Two),
                        CheckExprStmt(CheckOrExpression(One, Two)),
                        CheckExprStmt(CheckAndExpression(One, Two)),
                        CheckBinaryStmt(One, PythonOperator.In, Two),
                        CheckBinaryStmt(One, PythonOperator.NotIn, Two)
                    )
                );
            }
        }

        [TestMethod, Priority(0)]
        public void TrueDivide() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("TrueDivide.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckFromImport("__future__", new[] { "division" }),
                        CheckBinaryStmt(One, PythonOperator.TrueDivide, Two)
                    )
                );
            }
        }

        [TestMethod, Priority(0)]
        public void BinaryOperatorsV2() {
            foreach (var version in V2Versions) {
                CheckAst(
                    ParseFile("BinaryOperatorsV2.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckBinaryStmt(One, PythonOperator.NotEqual, Two)
                    )
                );
            }

            foreach (var version in V3Versions) {
                ParseErrors("BinaryOperatorsV2.py", version, new[] {
                    new ErrorResult("unexpected token '>'", new SourceSpan(1, 4, 1, 5)),
                    new ErrorResult("invalid syntax", new SourceSpan(1, 6, 1, 7))
                });
            }
        }

        [TestMethod, Priority(0)]
        public void MatMulOperator() {
            foreach (var version in V35AndUp) {
                CheckAst(
                    ParseFile("MatMulOperator.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckBinaryStmt(One, PythonOperator.MatMultiply, Two)
                    )
                );
            }

            foreach (var version in V3Versions.Except(V35AndUp)) {
                ParseErrors("MatMulOperator.py", version, new[] {
                    new ErrorResult("unexpected token '@'", new SourceSpan(1, 3, 1, 4))
                });
            }
        }

        [TestMethod, Priority(0)]
        public void GroupingRecovery() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("GroupingRecovery.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckAssignment(Fob, CheckParenExpr(CheckErrorExpr())),
                        CheckFuncDef("f", new Action<Parameter>[] {
                            p => {
                                Assert.AreEqual("a", p.Name);
                                Assert.AreEqual(13 + Environment.NewLine.Length, p.StartIndex);
                            }
                        }, CheckSuite(Pass))
                    )
                );
            }
        }

        [TestMethod, Priority(0)]
        public void GroupingRecoveryFailure() {
            // Align the "pass" keyword on a buffer border to ensure we restore the whitespace
            ParseString(new string(' ', Tokenizer.DefaultBufferCapacity - 9) + "{\r\n    pass", ErrorSink.Null, PythonLanguageVersion.V36);

            // Ensure we can restore whitespace that crosses buffer borders
            ParseString("{\r\n" + new string(' ', Tokenizer.DefaultBufferCapacity * 2 - 9) + "    pass", ErrorSink.Null, PythonLanguageVersion.V36);
        }

        [TestMethod, Priority(0)]
        public void UnaryOperators() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("UnaryOperators.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckUnaryStmt(PythonOperator.Negate, One),
                        CheckUnaryStmt(PythonOperator.Invert, One),
                        CheckUnaryStmt(PythonOperator.Pos, One),
                        CheckUnaryStmt(PythonOperator.Not, One)
                    )
                );
            }
        }

        [TestMethod, Priority(0)]
        public void StringPlus() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("StringPlus.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckStrOrBytesStmt(version, "hello again")
                    )
                );
            }
        }

        [TestMethod, Priority(0)]
        public void BytesPlus() {
            foreach (var version in V26AndUp) {
                CheckAst(
                    ParseFile("BytesPlus.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckExprStmt(CheckConstant(ToBytes("hello again")))
                    )
                );
            }

            foreach (var version in V24_V25Versions) {
                ParseErrors("BytesPlus.py", version,
                    new ErrorResult("invalid syntax", new SourceSpan(1, 2, 1, 9)),
                    new ErrorResult("unexpected token 'b'", new SourceSpan(1, 10, 1, 11))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void UnicodePlus() {
            foreach (var version in V2Versions.Concat(V33AndUp)) {
                CheckAst(
                    ParseFile("UnicodePlus.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckExprStmt(CheckConstant("hello again"))
                    )
                );
            }

            foreach (var version in V30_V32Versions) {
                ParseErrors("UnicodePlus.py", version,
                    new ErrorResult("invalid syntax", new SourceSpan(1, 2, 1, 9)),
                    new ErrorResult("unexpected token 'u'", new SourceSpan(1, 10, 1, 11))
                );

            }
        }

        [TestMethod, Priority(0)]
        public void RawBytes() {
            foreach (var version in V33AndUp) {
                CheckAst(
                    ParseFile("RawBytes.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckExprStmt(CheckConstant(ToBytes("\\fob"))),
                        CheckExprStmt(CheckConstant(ToBytes("\\fob"))),
                        CheckExprStmt(CheckConstant(ToBytes("\\fob"))),
                        CheckExprStmt(CheckConstant(ToBytes("\\fob"))),
                        CheckExprStmt(CheckConstant(ToBytes("\\fob"))),
                        CheckExprStmt(CheckConstant(ToBytes("\\fob"))),
                        CheckExprStmt(CheckConstant(ToBytes("\\fob"))),
                        CheckExprStmt(CheckConstant(ToBytes("\\fob"))),
                        CheckExprStmt(CheckConstant(ToBytes("\\fob"))),
                        CheckExprStmt(CheckConstant(ToBytes("\\fob"))),
                        CheckExprStmt(CheckConstant(ToBytes("\\fob"))),
                        CheckExprStmt(CheckConstant(ToBytes("\\fob"))),
                        CheckExprStmt(CheckConstant(ToBytes("\\fob"))),
                        CheckExprStmt(CheckConstant(ToBytes("\\fob"))),
                        CheckExprStmt(CheckConstant(ToBytes("\\fob"))),
                        CheckExprStmt(CheckConstant(ToBytes("\\fob")))
                    )
                );
            }

            foreach (var version in AllVersions.Except(V33AndUp)) {
                ParseErrors("RawBytes.py", version,
                    new ErrorResult("invalid syntax", new SourceSpan(1, 3, 1, 9)),
                    new ErrorResult("invalid syntax", new SourceSpan(2, 3, 2, 13)),
                    new ErrorResult("invalid syntax", new SourceSpan(3, 3, 3, 9)),
                    new ErrorResult("invalid syntax", new SourceSpan(4, 3, 4, 13)),
                    new ErrorResult("invalid syntax", new SourceSpan(5, 3, 5, 9)),
                    new ErrorResult("invalid syntax", new SourceSpan(6, 3, 6, 13)),
                    new ErrorResult("invalid syntax", new SourceSpan(7, 3, 7, 9)),
                    new ErrorResult("invalid syntax", new SourceSpan(8, 3, 8, 13)),
                    new ErrorResult("invalid syntax", new SourceSpan(9, 3, 9, 9)),
                    new ErrorResult("invalid syntax", new SourceSpan(10, 3, 10, 13)),
                    new ErrorResult("invalid syntax", new SourceSpan(11, 3, 11, 9)),
                    new ErrorResult("invalid syntax", new SourceSpan(12, 3, 12, 13)),
                    new ErrorResult("invalid syntax", new SourceSpan(13, 3, 13, 9)),
                    new ErrorResult("invalid syntax", new SourceSpan(14, 3, 14, 13)),
                    new ErrorResult("invalid syntax", new SourceSpan(15, 3, 15, 9)),
                    new ErrorResult("invalid syntax", new SourceSpan(16, 3, 16, 13))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void Delimiters() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("Delimiters.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckCallStmt(One, PositionalArg(Two)),
                        CheckIndexStmt(One, Two),
                        CheckDictionaryStmt(DictItem(One, Two)),
                        CheckTupleStmt(One, Two, Three),
                        CheckIndexStmt(One, CheckSlice(Two, Three)),
                        CheckIndexStmt(One, CheckSlice(Two, Three, Four)),
                        CheckIndexStmt(One, CheckSlice(Two, null, Four)),
                        CheckIndexStmt(One, CheckSlice(null, null, Four)),
                        CheckIndexStmt(One, Ellipsis),
                        CheckIndexStmt(One, CheckTupleExpr(CheckSlice(null, null))),
                        CheckMemberStmt(Fob, "oar"),
                        CheckAssignment(Fob, One),
                        CheckAssignment(Fob, PythonOperator.Add, One),
                        CheckAssignment(Fob, PythonOperator.Subtract, One),
                        CheckAssignment(Fob, PythonOperator.Multiply, One),
                        CheckAssignment(Fob, version.Is3x() ? PythonOperator.TrueDivide : PythonOperator.Divide, One),
                        CheckAssignment(Fob, PythonOperator.FloorDivide, One),
                        CheckAssignment(Fob, PythonOperator.Mod, One),
                        CheckAssignment(Fob, PythonOperator.BitwiseAnd, One),
                        CheckAssignment(Fob, PythonOperator.BitwiseOr, One),
                        CheckAssignment(Fob, PythonOperator.Xor, One),
                        CheckAssignment(Fob, PythonOperator.RightShift, One),
                        CheckAssignment(Fob, PythonOperator.LeftShift, One),
                        CheckAssignment(Fob, PythonOperator.Power, One)
                    )
                );
            }
        }

        [TestMethod, Priority(0)]
        public void DelimitersV2() {
            foreach (var version in V2Versions) {
                CheckAst(
                    ParseFile("DelimitersV2.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckBackquoteStmt(Fob)
                    )
                );
            }

            foreach (var version in V3Versions) {
                ParseErrors(
                    "DelimitersV2.py",
                    version,
                    new[] {
                        new ErrorResult("unexpected token '`'", new SourceSpan(1, 1, 1, 2)),
                        new ErrorResult("unexpected token 'fob'", new SourceSpan(1, 2, 1, 5)),
                        new ErrorResult("unexpected token '`'", new SourceSpan(1, 5, 1, 6))
                   }
                );
            }
        }

        [TestMethod, Priority(0)]
        public void ForStmt() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("ForStmt.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckForStmt(Fob, Oar, CheckSuite(Pass)),
                        CheckForStmt(CheckTupleExpr(Fob, Oar), Baz, CheckSuite(Pass)),
                        CheckForStmt(Fob, Oar, CheckSuite(Pass), CheckSuite(Pass)),
                        CheckForStmt(Fob, Oar, CheckSuite(Break)),
                        CheckForStmt(Fob, Oar, CheckSuite(Continue)),
                        CheckForStmt(CheckListExpr(CheckListExpr(Fob), CheckListExpr(Oar)), Baz, CheckSuite(Pass)),
                        CheckForStmt(CheckTupleExpr(CheckParenExpr(Fob), CheckParenExpr(Oar)), Baz, CheckSuite(Pass))
                    )
                );
            }
        }

        [TestMethod, Priority(0)]
        public void WithStmt() {
            foreach (var version in V26AndUp) {
                CheckAst(
                    ParseFile("WithStmt.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckWithStmt(Fob, CheckSuite(Pass)),
                        CheckWithStmt(Fob, Oar, CheckSuite(Pass)),
                        CheckWithStmt(new[] { Fob, Oar }, CheckSuite(Pass)),
                        CheckWithStmt(new[] { Fob, Baz }, new[] { Oar, Quox }, CheckSuite(Pass))
                    )
                );
            }

            foreach (var version in V24_V25Versions) {
                ParseErrors("WithStmt.py", version,
                    new ErrorResult("unexpected token 'fob'", new SourceSpan(1, 6, 1, 9)),
                    new ErrorResult("unexpected token ':'", new SourceSpan(1, 9, 1, 10)),
                    new ErrorResult("unexpected token 'pass'", new SourceSpan(1, 11, 1, 15)),
                    new ErrorResult("unexpected token 'fob'", new SourceSpan(3, 6, 3, 9)),
                    new ErrorResult("unexpected token 'oar'", new SourceSpan(3, 13, 3, 16)),
                    new ErrorResult("unexpected token ':'", new SourceSpan(3, 16, 3, 17)),
                    new ErrorResult("unexpected token 'pass'", new SourceSpan(3, 18, 3, 22)),
                    new ErrorResult("unexpected token 'fob'", new SourceSpan(5, 6, 5, 9)),
                    new ErrorResult("unexpected token ','", new SourceSpan(5, 9, 5, 10)),
                    new ErrorResult("unexpected token 'oar'", new SourceSpan(5, 11, 5, 14)),
                    new ErrorResult("unexpected token ':'", new SourceSpan(5, 14, 5, 15)),
                    new ErrorResult("unexpected token 'pass'", new SourceSpan(5, 16, 5, 20)),
                    new ErrorResult("unexpected token 'fob'", new SourceSpan(7, 6, 7, 9)),
                    new ErrorResult("unexpected token 'oar'", new SourceSpan(7, 13, 7, 16)),
                    new ErrorResult("unexpected token ','", new SourceSpan(7, 16, 7, 17)),
                    new ErrorResult("unexpected token 'baz'", new SourceSpan(7, 18, 7, 21)),
                    new ErrorResult("unexpected token 'quox'", new SourceSpan(7, 25, 7, 29)),
                    new ErrorResult("unexpected token ':'", new SourceSpan(7, 29, 7, 30)),
                    new ErrorResult("unexpected token 'pass'", new SourceSpan(7, 31, 7, 35))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void Semicolon() {
            foreach (var version in V26AndUp) {
                CheckAst(
                    ParseFile("Semicolon.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckSuite(
                            CheckConstantStmt(1),
                            CheckConstantStmt(2),
                            CheckConstantStmt(3)
                        ),
                        CheckSuite(
                            CheckNameStmt("fob"),
                            CheckNameStmt("oar"),
                            CheckNameStmt("baz")
                        )
                    )
                );
            }
        }

        [TestMethod, Priority(0)]
        public void DelStmt() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("DelStmt.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckDelStmt(Fob),
                        CheckDelStmt(Fob, Oar),
                        CheckDelStmt(CheckMemberExpr(Fob, "oar")),
                        CheckDelStmt(CheckIndexExpression(Fob, Oar)),
                        CheckDelStmt(CheckTupleExpr(Fob, Oar)),
                        CheckDelStmt(CheckListExpr(Fob, Oar)),
                        CheckDelStmt(CheckParenExpr(Fob))
                    )
                );
            }
        }

        [TestMethod, Priority(0)]
        public void IndexExpr() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("IndexExpr.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckExprStmt(CheckIndexExpression(Fob, CheckConstant(.2)))
                    )
                );
            }
        }

        [TestMethod, Priority(0)]
        public void DelStmtIllegal() {
            foreach (var version in AllVersions) {
                ParseErrors("DelStmtIllegal.py", version,
                    new ErrorResult("can't delete literal", new SourceSpan(1, 5, 1, 6)),
                    new ErrorResult("can't delete generator expression", new SourceSpan(2, 5, 2, 25)),
                    new ErrorResult("can't delete function call", new SourceSpan(3, 5, 3, 13))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void YieldStmt() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("YieldStmt.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckFuncDef("f", NoParameters,
                            CheckSuite(
                                CheckYieldStmt(One),
                                CheckYieldStmt(CheckTupleExpr(One, Two))
                            )
                        )
                    )
                );
            }
        }

        [TestMethod, Priority(0)]
        public void YieldExpr() {
            foreach (var version in V26AndUp) {
                CheckAst(
                    ParseFile("YieldExpr.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckFuncDef("f", NoParameters, CheckSuite(
                            CheckYieldStmt(None)
                        )),
                        CheckFuncDef("f", NoParameters, CheckSuite(
                            CheckAssignment(Fob, CheckYieldExpr(None))
                        )),
                        CheckFuncDef("f", NoParameters, CheckSuite(
                            CheckAssignment(Baz, CheckListComp(CheckParenExpr(CheckYieldExpr(Oar)), CompFor(Oar, Fob)))
                        ))
                    )
                );
            }

            ParseErrors("YieldExpr.py", PythonLanguageVersion.V24,
                new ErrorResult("invalid syntax", new SourceSpan(2, 10, 3, 1)),
                new ErrorResult("unexpected token 'yield'", new SourceSpan(5, 11, 5, 16))
            // [(yield oar) for ...] should be an error, but it is not raised.
            // V24 is not supported by PTVS, so don't fail the test because of this.
            //new ErrorResult("unexpected token 'yield'", new SourceSpan(8, 13, 8, 18))
            );
        }

        [TestMethod, Priority(0)]
        public void YieldStmtIllegal() {
            foreach (var version in V2Versions.Concat(V30_V32Versions)) {
                ParseErrors("YieldStmtIllegal.py", version,
                    new ErrorResult("misplaced yield", new SourceSpan(1, 1, 1, 6)),
                    new ErrorResult("'return' with argument inside generator", new SourceSpan(4, 5, 4, 14)),
                    new ErrorResult("'return' with argument inside generator", new SourceSpan(9, 5, 9, 14))
                );
            }

            // return inside generator is legal as of 3.3
            foreach (var version in V33AndUp) {
                ParseErrors("YieldStmtIllegal.py", version,
                    new ErrorResult("misplaced yield", new SourceSpan(1, 1, 1, 6))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void YieldFromStmt() {
            foreach (var version in V33AndUp) {
                CheckAst(
                    ParseFile("YieldFromStmt.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckFuncDef("f", NoParameters,
                            CheckSuite(
                                CheckYieldFromStmt(Fob)
                            )
                        )
                    )
                );
            }
        }

        [TestMethod, Priority(0)]
        public void YieldFromExpr() {
            foreach (var version in V33AndUp) {
                CheckAst(
                    ParseFile("YieldFromExpr.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckFuncDef("f", NoParameters,
                            CheckSuite(
                                CheckYieldFromStmt(Fob),
                                CheckAssignment(Oar, CheckYieldFromExpr(Fob)),
                                CheckAssignment(Baz, CheckListComp(CheckParenExpr(CheckYieldFromExpr(Oar)), CompFor(Oar, Fob)))
                            )
                        )
                    )
                );
            }

            foreach (var version in V25_V27Versions.Concat(V30_V32Versions)) {
                ParseErrors("YieldFromExpr.py", version,
                    new ErrorResult("invalid syntax", new SourceSpan(2, 11, 2, 15)),
                    new ErrorResult("invalid syntax", new SourceSpan(3, 17, 3, 21)),
                    new ErrorResult("invalid syntax", new SourceSpan(4, 19, 4, 23))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void YieldFromStmtIllegal() {
            foreach (var version in V25_V27Versions.Concat(V30_V32Versions)) {
                if (version == PythonLanguageVersion.V24) {
                    continue;
                }
                ParseErrors("YieldFromStmtIllegal.py", version,
                    new ErrorResult("misplaced yield", new SourceSpan(1, 1, 1, 6)),
                    new ErrorResult("invalid syntax", new SourceSpan(1, 7, 1, 11)),
                    new ErrorResult("'return' with argument inside generator", new SourceSpan(4, 5, 4, 14)),
                    new ErrorResult("invalid syntax", new SourceSpan(5, 11, 5, 15)),
                    new ErrorResult("invalid syntax", new SourceSpan(8, 11, 8, 15)),
                    new ErrorResult("'return' with argument inside generator", new SourceSpan(9, 5, 9, 14)),
                    new ErrorResult("invalid syntax", new SourceSpan(12, 11, 12, 15)),
                    new ErrorResult("invalid syntax", new SourceSpan(15, 11, 15, 15))
                );
            }

            foreach (var version in V33AndUp) {
                ParseErrors("YieldFromStmtIllegal.py", version,
                    new ErrorResult("misplaced yield", new SourceSpan(1, 1, 1, 6)),
                    new ErrorResult("invalid syntax", new SourceSpan(12, 15, 13, 1)),
                    new ErrorResult("invalid syntax", new SourceSpan(15, 16, 15, 23))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void ImportStmt() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("ImportStmt.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckImport(new[] { "sys" }),
                        CheckImport(new[] { "sys", "fob" }),
                        CheckImport(new[] { "sys" }, new[] { "oar" }),
                        CheckImport(new[] { "sys", "fob" }, new[] { "oar", "baz" }),
                        CheckImport(new[] { "sys.fob" }),
                        CheckImport(new[] { "sys.fob" }, new[] { "oar" })
                    )
                );
            }

            foreach (var version in AllVersions) {
                ParseErrors("ImportStmtIllegal.py", version,
                    new ErrorResult("unexpected token '('", new SourceSpan(1, 18, 1, 19)),
                    new ErrorResult("unexpected token '('", new SourceSpan(1, 18, 1, 19)),
                    new ErrorResult("unexpected token '('", new SourceSpan(1, 18, 1, 19)),
                    new ErrorResult("unexpected token ')'", new SourceSpan(1, 25, 1, 26)),
                    new ErrorResult("unexpected token ')'", new SourceSpan(1, 26, 1, 27))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void GlobalStmt() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("GlobalStmt.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckFuncDef("f", NoParameters,
                            CheckSuite(
                                CheckGlobal("a"),
                                CheckGlobal("a", "b")
                            )
                        )
                    )
                );
            }
        }

        [TestMethod, Priority(0)]
        public void NonlocalStmt() {
            foreach (var version in V3Versions) {
                CheckAst(
                    ParseFile("NonlocalStmt.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckFuncDef("g", NoParameters,
                            CheckSuite(
                                CheckAssignment(Fob, One),
                                CheckAssignment(Oar, One),
                                CheckFuncDef("f", NoParameters,
                                    CheckSuite(
                                        CheckNonlocal("fob"),
                                        CheckNonlocal("fob", "oar")
                                    )
                                )
                            )
                        ),
                        CheckFuncDef("g", NoParameters,
                            CheckSuite(
                                CheckFuncDef("f", NoParameters,
                                    CheckSuite(
                                        CheckNonlocal("fob")
                                    )
                                ),
                                CheckAssignment(Fob, One)
                            )
                        ),
                        CheckFuncDef("f", NoParameters,
                            CheckSuite(
                                CheckClassDef("C",
                                    CheckSuite(
                                        CheckNonlocal("fob"),
                                        CheckAssignment(Fob, One)
                                    )
                                ),
                                CheckAssignment(Fob, Two)
                            )
                        ),
                        CheckClassDef("X",
                            CheckSuite(
                                CheckFuncDef("f", new[] { CheckParameter("x") },
                                    CheckSuite(
                                        CheckNonlocal("__class__")
                                    )
                                )
                            )
                        )
                    )
                );
            }

            foreach (var version in V2Versions) {
                ParseErrors("NonlocalStmt.py", version,
                    new ErrorResult("unexpected token 'fob'", new SourceSpan(5, 18, 5, 21)),
                    new ErrorResult("unexpected token '<newline>'", new SourceSpan(5, 21, 6, 9)),
                    new ErrorResult("unexpected token 'nonlocal'", new SourceSpan(6, 9, 6, 17)),
                    new ErrorResult("unexpected token 'fob'", new SourceSpan(11, 18, 11, 21)),
                    new ErrorResult("unexpected token '<newline>'", new SourceSpan(11, 21, 12, 1)),
                    new ErrorResult("unexpected token '<NL>'", new SourceSpan(12, 1, 13, 5)),
                    new ErrorResult("unexpected token 'fob'", new SourceSpan(18, 18, 18, 21)),
                    new ErrorResult("unexpected token '<newline>'", new SourceSpan(18, 21, 19, 9)),
                    new ErrorResult("unexpected token 'fob'", new SourceSpan(19, 9, 19, 12)),
                    new ErrorResult("unexpected token '='", new SourceSpan(19, 13, 19, 14)),
                    new ErrorResult("unexpected token '1'", new SourceSpan(19, 15, 19, 16)),
                    new ErrorResult("unexpected token '<newline>'", new SourceSpan(19, 16, 20, 5)),
                    new ErrorResult("unexpected token '<dedent>'", new SourceSpan(19, 16, 20, 5)),
                    new ErrorResult("unexpected token '__class__'", new SourceSpan(24, 18, 24, 27)),
                    new ErrorResult("unexpected token '<newline>'", new SourceSpan(24, 27, 25, 1)),
                    new ErrorResult("unexpected token '<dedent>'", new SourceSpan(24, 27, 25, 1)),
                    new ErrorResult("unexpected end of file", new SourceSpan(25, 1, 25, 1)),
                    new ErrorResult("unexpected end of file", new SourceSpan(25, 1, 25, 1))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void NonlocalStmtIllegal() {
            foreach (var version in V3Versions) {
                ParseErrors("NonlocalStmtIllegal.py", version,
                    new ErrorResult("nonlocal declaration not allowed at module level", new SourceSpan(17, 1, 17, 9)),
                    new ErrorResult("name 'x' is nonlocal and global", new SourceSpan(10, 13, 10, 23)),
                    new ErrorResult("name 'x' is a parameter and nonlocal", new SourceSpan(15, 13, 15, 23)),
                    new ErrorResult("no binding for nonlocal 'x' found", new SourceSpan(35, 22, 35, 23)),
                    new ErrorResult("no binding for nonlocal 'x' found", new SourceSpan(27, 12, 27, 13)),
                    new ErrorResult("no binding for nonlocal 'globalvar' found", new SourceSpan(21, 14, 21, 23)),
                    new ErrorResult("no binding for nonlocal 'a' found", new SourceSpan(3, 18, 3, 19))
                );
            }

        }

        [TestMethod, Priority(0)]
        public void WhileStmt() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("WhileStmt.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckWhileStmt(One, CheckSuite(Pass)),
                        CheckWhileStmt(One, CheckSuite(Pass), CheckSuite(Pass))
                    )
                );
            }
        }

        [TestMethod, Priority(0)]
        public void TryStmt() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("TryStmt.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckTryStmt(
                            CheckSuite(Pass),
                            new[] { CheckHandler(null, null, CheckSuite(Pass)) }
                        ),
                        CheckTryStmt(
                            CheckSuite(Pass),
                            new[] { CheckHandler(Exception, null, CheckSuite(Pass)) }
                        )
                    )
                );
            }

            // execpt Exception as e: vs except Exception, e:
            // comma supported in 2.4/2.5, both supported in 2.6 - 2.7, as supported in 3.x
            foreach (var version in V24_V25Versions) {
                TryStmtV2(version);

                ParseErrors(
                    "TryStmtV3.py", version,
                    new ErrorResult("'as' requires Python 2.6 or later", new SourceSpan(3, 18, 3, 20))
                );
            }

            foreach (var version in V26_V27Versions) {
                TryStmtV2(version);
                TryStmtV3(version);
            }

            foreach (var version in V3Versions) {
                TryStmtV3(version);

                ParseErrors(
                    "TryStmtV2.py", version,
                    new ErrorResult("\", variable\" not allowed in 3.x - use \"as variable\" instead.", new SourceSpan(3, 17, 3, 20))
                );
            }
        }

        private void TryStmtV3(PythonLanguageVersion version) {
            CheckAst(
                ParseFile("TryStmtV3.py", ErrorSink.Null, version),
                CheckSuite(
                    CheckTryStmt(
                        CheckSuite(Pass),
                        new[] { CheckHandler(Exception, CheckNameExpr("e"), CheckSuite(Pass)) }
                    )
                )
            );
        }

        private void TryStmtV2(PythonLanguageVersion version) {
            CheckAst(
                ParseFile("TryStmtV2.py", ErrorSink.Null, version),
                CheckSuite(
                    CheckTryStmt(
                        CheckSuite(Pass),
                        new[] { CheckHandler(Exception, CheckNameExpr("e"), CheckSuite(Pass)) }
                    )
                )
            );
        }

        [TestMethod, Priority(0)]
        public void RaiseStmt() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("RaiseStmt.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckRaiseStmt(),
                        CheckRaiseStmt(Fob)
                    )
                );
            }

            foreach (var version in V2Versions) {
                CheckAst(
                    ParseFile("RaiseStmtV2.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckRaiseStmt(Fob, Oar),
                        CheckRaiseStmt(Fob, Oar, Baz)
                    )
                );

                ParseErrors(
                    "RaiseStmtV3.py", version,
                    new ErrorResult("invalid syntax, from cause not allowed in 2.x.", new SourceSpan(1, 11, 1, 19))
                );
            }

            foreach (var version in V3Versions) {
                CheckAst(
                    ParseFile("RaiseStmtV3.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckRaiseStmt(Fob, cause: Oar)
                    )
                );

                ParseErrors(
                    "RaiseStmtV2.py", version,
                    new ErrorResult("invalid syntax, only exception value is allowed in 3.x.", new SourceSpan(1, 10, 1, 15)),
                    new ErrorResult("invalid syntax, only exception value is allowed in 3.x.", new SourceSpan(2, 10, 2, 15))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void PrintStmt() {
            foreach (var version in V2Versions) {
                CheckAst(
                    ParseFile("PrintStmt.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckPrintStmt(new Action<Expression>[0]),
                        CheckPrintStmt(new[] { One }),
                        CheckPrintStmt(new[] { One }, trailingComma: true),
                        CheckPrintStmt(new[] { One, Two }),
                        CheckPrintStmt(new[] { One, Two }, trailingComma: true),
                        CheckPrintStmt(new[] { One, Two }, Fob),
                        CheckPrintStmt(new[] { One, Two }, Fob, trailingComma: true),
                        CheckPrintStmt(new Action<Expression>[0], Fob),
                        CheckPrintStmt(new[] { CheckBinaryExpression(One, PythonOperator.Equal, Two) }),
                        CheckPrintStmt(new[] { CheckLambda(new Action<Parameter>[0], One) })
                    )
                );
            }

            foreach (var version in V3Versions) {
                ParseErrors(
                    "PrintStmt.py", version,
                    new ErrorResult("invalid syntax", new SourceSpan(2, 7, 2, 8)),
                    new ErrorResult("invalid syntax", new SourceSpan(3, 7, 3, 8)),
                    new ErrorResult("invalid syntax", new SourceSpan(4, 7, 4, 8)),
                    new ErrorResult("invalid syntax", new SourceSpan(5, 7, 5, 8)),
                    new ErrorResult("invalid syntax", new SourceSpan(9, 7, 9, 8)),
                    new ErrorResult("unexpected token 'lambda'", new SourceSpan(10, 7, 10, 13)),
                    new ErrorResult("unexpected token ':'", new SourceSpan(10, 13, 10, 14)),
                    new ErrorResult("unexpected token '1'", new SourceSpan(10, 15, 10, 16))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void AssertStmt() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("AssertStmt.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckAssertStmt(One),
                        CheckAssertStmt(One, Fob)
                    )
                );
            }
        }

        [TestMethod, Priority(0)]
        public void ListComp() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("ListComp.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckExprStmt(CheckListComp(Fob, CompFor(Fob, Oar))),
                        CheckExprStmt(CheckListComp(Fob, CompFor(Fob, Oar), CompIf(Baz))),
                        CheckExprStmt(CheckListComp(Fob, CompFor(Fob, Oar), CompFor(Baz, Quox)))
                    )
                );
            }
        }

        [TestMethod, Priority(0)]
        public void ListComp2x() {
            foreach (var version in V2Versions) {
                CheckAst(
                    ParseFile("ListComp2x.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckExprStmt(CheckListComp(Fob, CompFor(Fob, CheckTupleExpr(Oar, Baz))))
                    )
                );
            }

            foreach (var version in V3Versions) {
                ParseErrors("ListComp2x.py", version,
                    new ErrorResult("unexpected token ','", new SourceSpan(1, 20, 1, 21)),
                    new ErrorResult("unexpected token ']'", new SourceSpan(1, 25, 1, 26))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void GenComp() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("GenComp.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckExprStmt(CheckGeneratorComp(Fob, CompFor(Fob, Oar))),
                        CheckExprStmt(CheckGeneratorComp(Fob, CompFor(Fob, Oar), CompIf(Baz))),
                        CheckExprStmt(CheckGeneratorComp(Fob, CompFor(Fob, Oar), CompFor(Baz, Quox))),
                        CheckCallStmt(Baz, PositionalArg(CheckGeneratorComp(Fob, CompFor(Fob, Oar))))
                    )
                );
            }
        }

        [TestMethod, Priority(0)]
        public void DictComp() {
            foreach (var version in V27AndUp) {
                CheckAst(
                    ParseFile("DictComp.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckExprStmt(CheckDictComp(Fob, Oar, CompFor(CheckTupleExpr(Fob, Oar), Baz))),
                        CheckExprStmt(CheckDictComp(Fob, Oar, CompFor(CheckTupleExpr(Fob, Oar), Baz), CompIf(Quox))),
                        CheckExprStmt(CheckDictComp(Fob, Oar, CompFor(CheckTupleExpr(Fob, Oar), Baz), CompFor(Quox, Exception)))
                    )
                );
            }

            foreach (var version in V24_V26Versions) {
                ParseErrors("DictComp.py", version,
                    new ErrorResult("invalid syntax", new SourceSpan(1, 10, 1, 13)),
                    new ErrorResult("invalid syntax", new SourceSpan(2, 10, 2, 13)),
                    new ErrorResult("invalid syntax", new SourceSpan(3, 10, 3, 13))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void SetComp() {
            foreach (var version in V27AndUp) {
                CheckAst(
                    ParseFile("SetComp.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckExprStmt(CheckSetComp(Fob, CompFor(Fob, Baz))),
                        CheckExprStmt(CheckSetComp(Fob, CompFor(Fob, Baz), CompIf(Quox))),
                        CheckExprStmt(CheckSetComp(Fob, CompFor(Fob, Baz), CompFor(Quox, Exception)))
                    )
                );
            }

            foreach (var version in V24_V26Versions) {
                ParseErrors("SetComp.py", version,
                    new ErrorResult("invalid syntax, set literals require Python 2.7 or later.", new SourceSpan(1, 2, 1, 5)),
                    new ErrorResult("invalid syntax, set literals require Python 2.7 or later.", new SourceSpan(2, 2, 2, 5)),
                    new ErrorResult("invalid syntax, set literals require Python 2.7 or later.", new SourceSpan(3, 2, 3, 5))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void SetLiteral() {
            foreach (var version in V27AndUp) {
                CheckAst(
                    ParseFile("SetLiteral.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckExprStmt(CheckSetLiteral(One)),
                        CheckExprStmt(CheckSetLiteral(One, Two))
                    )
                );
            }

            foreach (var version in V24_V26Versions) {
                ParseErrors("SetLiteral.py", version,
                    new ErrorResult("invalid syntax, set literals require Python 2.7 or later.", new SourceSpan(1, 2, 1, 3)),
                    new ErrorResult("invalid syntax, set literals require Python 2.7 or later.", new SourceSpan(2, 2, 2, 3))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void IfStmt() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("IfStmt.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckIfStmt(IfTests(IfTest(One, CheckSuite(Pass)))),
                        CheckIfStmt(IfTests(IfTest(One, CheckSuite(Pass)), IfTest(Two, CheckSuite(Pass)))),
                        CheckIfStmt(IfTests(IfTest(One, CheckSuite(Pass))), CheckSuite(Pass))
                    )
                );
            }
        }

        [TestMethod, Priority(0)]
        public void FromImportStmt() {

            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("FromImportStmt.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckFromImport("sys", new[] { "winver" }),
                        CheckFromImport("sys", new[] { "winver" }, new[] { "baz" }),
                        CheckFromImport("sys.fob", new[] { "winver" }),
                        CheckFromImport("sys.fob", new[] { "winver" }, new[] { "baz" }),
                        CheckFromImport("...fob", new[] { "oar" }),
                        CheckFromImport("....fob", new[] { "oar" }),
                        CheckFromImport("......fob", new[] { "oar" }),
                        CheckFromImport(".......fob", new[] { "oar" }),
                        CheckFromImport("fob", new[] { "fob", "baz" }, new string[] { "oar", "quox" })
                    )
                );
            }

            foreach (var version in V2Versions) {
                CheckAst(
                    ParseFile("FromImportStmtV2.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckFuncDef("f", NoParameters,
                            CheckSuite(CheckFromImport("sys", new[] { "*" }))
                        ),
                        CheckClassDef("C",
                            CheckSuite(CheckFromImport("sys", new[] { "*" }))
                        )
                    )
                );
            }

            foreach (var version in V3Versions) {
                ParseErrors(
                    "FromImportStmtV2.py",
                    version,
                    new ErrorResult("import * only allowed at module level", new SourceSpan(2, 21, 2, 22)),
                    new ErrorResult("import * only allowed at module level", new SourceSpan(5, 21, 5, 22))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void FromImportStmtIllegal() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("FromImportStmtIllegal.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckFromImport("", new[] { "fob" })
                    )
                );

                ParseErrors(
                    "FromImportStmtIllegal.py",
                    version,
                    new ErrorResult("missing module name", new SourceSpan(1, 6, 1, 12))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void FromImportStmtIncomplete() {

            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("FromImportStmtIncomplete.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckFuncDef(
                            "f",
                            NoParameters,
                            CheckSuite(
                                CheckFromImport("sys", new[] { "abc", "" })
                            )
                        )
                    )
                );

                ParseErrors(
                    "FromImportStmtIncomplete.py",
                    version,
                    new ErrorResult("unexpected token '<newline>'", new SourceSpan(2, 26, 3, 1))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void DecoratorsFuncDef() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("DecoratorsFuncDef.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckFuncDef("f", NoParameters, CheckSuite(Pass), new[] { Fob }),
                        CheckFuncDef("f", NoParameters, CheckSuite(Pass), new[] { CheckMemberExpr(Fob, "oar") }),
                        CheckFuncDef("f", NoParameters, CheckSuite(Pass), new[] { CheckCallExpression(Fob, PositionalArg(Oar)) }),
                        CheckFuncDef("f", NoParameters, CheckSuite(Pass), new[] { Fob, Oar })
                    )
                );
            }
        }

        [TestMethod, Priority(0)]
        public void DecoratorsAsyncFuncDef() {
            foreach (var version in V35AndUp) {
                CheckAst(
                    ParseFileNoErrors("DecoratorsAsyncFuncDef.py", version),
                    CheckSuite(
                        CheckCoroutineDef(CheckFuncDef("f", NoParameters, CheckSuite(Pass), new[] { Fob })),
                        CheckCoroutineDef(CheckFuncDef("f", NoParameters, CheckSuite(Pass), new[] { CheckMemberExpr(Fob, "oar") })),
                        CheckCoroutineDef(CheckFuncDef("f", NoParameters, CheckSuite(Pass), new[] { CheckCallExpression(Fob, PositionalArg(Oar)) })),
                        CheckCoroutineDef(CheckFuncDef("f", NoParameters, CheckSuite(Pass), new[] { Fob, Oar }))
                    )
                );
            }
        }

        [TestMethod, Priority(0)]
        public void DecoratorsClassDef() {
            foreach (var version in V26AndUp) {
                CheckAst(
                    ParseFile("DecoratorsClassDef.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckClassDef("C", CheckSuite(Pass), decorators: new[] { Fob }),
                        CheckClassDef("C", CheckSuite(Pass), decorators: new[] { CheckMemberExpr(Fob, "oar") }),
                        CheckClassDef("C", CheckSuite(Pass), decorators: new[] { CheckCallExpression(Fob, PositionalArg(Oar)) }),
                        CheckClassDef("C", CheckSuite(Pass), decorators: new[] { Fob, Oar })
                    )
                );
            }

            foreach (var version in V24_V25Versions) {
                ParseErrors("DecoratorsClassDef.py",
                    version,
                    new ErrorResult("invalid syntax, class decorators require 2.6 or later.", new SourceSpan(2, 1, 2, 6)),
                    new ErrorResult("invalid syntax, class decorators require 2.6 or later.", new SourceSpan(5, 1, 5, 6)),
                    new ErrorResult("invalid syntax, class decorators require 2.6 or later.", new SourceSpan(9, 1, 9, 6)),
                    new ErrorResult("invalid syntax, class decorators require 2.6 or later.", new SourceSpan(13, 1, 13, 6))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void DecoratorsIllegal() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("DecoratorsIllegal.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckErrorStmt(),
                        CheckAssignment(Fob, One)
                    )
                );
            }

            foreach (var version in AllVersions) {
                ParseErrors("DecoratorsIllegal.py",
                    version,
                    new ErrorResult("unexpected token 'fob'", new SourceSpan(2, 1, 2, 4))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void Calls() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("Calls.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckCallStmt(Fob),
                        CheckCallStmt(Fob, PositionalArg(One)),
                        CheckCallStmt(Fob, NamedArg("oar", One)),
                        CheckCallStmt(Fob, ListArg(Oar)),
                        CheckCallStmt(Fob, DictArg(Oar)),
                        CheckCallStmt(Fob, ListArg(Oar), DictArg(Baz)),
                        CheckCallStmt(Fob, NamedArg("oar", One), NamedArg("baz", Two)),
                        CheckCallStmt(Fob, PositionalArg(Oar), PositionalArg(Baz))
                    )
                );
            }
        }

        [TestMethod, Priority(0)]
        public void CallsIllegal() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("CallsIllegal.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckCallStmt(Fob, NamedArg("oar", One), NamedArg("oar", Two)),
                        CheckCallStmt(Fob, NamedArg(null, Two))
                    )
                );
            }

            foreach (var version in AllVersions) {
                ParseErrors("CallsIllegal.py",
                    version,
                    new ErrorResult("duplicate keyword argument", new SourceSpan(1, 21, 1, 22)),
                    new ErrorResult("expected name", new SourceSpan(2, 5, 2, 6))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void LambdaExpr() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("LambdaExpr.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckLambdaStmt(new[] { CheckParameter("x") }, One),
                        CheckLambdaStmt(new[] { CheckParameter("x", ParameterKind.List) }, One),
                        CheckLambdaStmt(new[] { CheckParameter("x", ParameterKind.Dictionary) }, One),
                        CheckLambdaStmt(NoParameters, One)
                    )
                );
            }
        }

        [TestMethod, Priority(0)]
        public void LambdaExprEmpty() {
            foreach (var version in AllVersions) {
                var errors = new CollectingErrorSink();
                CheckAst(
                    ParseString("lambda", errors, version),
                    CheckSuite(
                        CheckLambdaStmt(NoParameters, CheckErrorExpr())
                    )
                );

                errors.Errors.Should().BeEquivalentTo(new[] {
                    new ErrorResult("expected ':'", new SourceSpan(1, 7, 1, 7))
                });
            }
        }

        [TestMethod, Priority(0)]
        public void LambdaErrors() {
            foreach (var version in AllVersions) {
                var errors = new CollectingErrorSink();
                CheckAst(
                    ParseFile("LambdaErrors.py", errors, version),
                    CheckSuite(
                        CheckLambdaStmt(new[] {
                            CheckParameter(null, ParameterKind.Normal, null, null)
                        }, CheckErrorExpr()),
                        CheckLambdaStmt(new[] {
                            CheckParameter("x"),
                            CheckParameter("y")
                        }, CheckErrorExpr()),
                        CheckLambdaStmt(new[] {
                            CheckParameter("x"),
                        }, CheckErrorExpr())
                    )
                );

                errors.Errors.Should().BeEquivalentTo(new[] {
                    new ErrorResult("unexpected token '<newline>'", new SourceSpan(1, 7, 2, 1)),
                    new ErrorResult("invalid parameter", new SourceSpan(1, 1, 1, 7)),
                    new ErrorResult("expected ':'", new SourceSpan(1, 7, 1, 7)),
                    new ErrorResult("expected ':'", new SourceSpan(2, 12, 2, 12)),
                    new ErrorResult("expected ':'", new SourceSpan(3, 10, 3, 10)),
                    new ErrorResult("unexpected token '1'", new SourceSpan(3, 10, 3, 11))
                });
            }
        }


        [TestMethod, Priority(0)]
        public void FuncDef() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("FuncDef.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckFuncDef("f", NoParameters, CheckSuite(Pass)),
                        CheckFuncDef("f", new[] { CheckParameter("a") }, CheckSuite(Pass)),
                        CheckFuncDef("f", new[] { CheckParameter("a"), CheckParameter("b", ParameterKind.List) }, CheckSuite(Pass)),
                        CheckFuncDef("f", new[] { CheckParameter("a"), CheckParameter("b", ParameterKind.List), CheckParameter("c", ParameterKind.Dictionary) }, CheckSuite(Pass)),
                        CheckFuncDef("f", new[] { CheckParameter("a", ParameterKind.List) }, CheckSuite(Pass)),
                        CheckFuncDef("f", new[] { CheckParameter("a", ParameterKind.Dictionary) }, CheckSuite(Pass)),

                        CheckFuncDef("f", NoParameters, CheckSuite(CheckReturnStmt(One))),
                        CheckFuncDef("f", NoParameters, CheckSuite(CheckReturnStmt()))
                    )
                );
            }
        }

        [TestMethod, Priority(0)]
        public void FuncDefV2() {
            foreach (var version in V2Versions) {
                CheckAst(
                    ParseFileNoErrors("FuncDefV2.py", version),
                    CheckSuite(
                        CheckFuncDef("f", new[] { CheckParameter("a"), CheckSublistParameter("b", "c"), CheckParameter("d") }, CheckSuite(Pass))
                    )
                );
            }

            foreach (var version in V3Versions) {
                ParseErrors("FuncDefV2.py", version,
                    new ErrorResult("sublist parameters are not supported in 3.x", new SourceSpan(1, 10, 1, 16))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void FuncDefV3() {
            foreach (var version in V3Versions) {
                CheckAst(
                    ParseFileNoErrors("FuncDefV3.py", version),
                    CheckSuite(
                        CheckFuncDef("f", new[] { CheckParameter("a", ParameterKind.List), CheckParameter("x", ParameterKind.KeywordOnly) }, CheckSuite(Pass)),
                        CheckFuncDef("f", new[] { CheckParameter("a", ParameterKind.List), CheckParameter("x", ParameterKind.KeywordOnly, defaultValue: One) }, CheckSuite(Pass)),

                        CheckFuncDef("f", new[] { CheckParameter("a", annotation: One) }, CheckSuite(Pass)),
                        CheckFuncDef("f", new[] { CheckParameter("a", ParameterKind.List, annotation: One) }, CheckSuite(Pass)),
                        CheckFuncDef("f", new[] { CheckParameter("a", ParameterKind.Dictionary, annotation: One) }, CheckSuite(Pass)),
                        CheckFuncDef("f", new[] { CheckParameter("a", annotation: Zero), CheckParameter("b", ParameterKind.List, annotation: One), CheckParameter("c", ParameterKind.Dictionary, annotation: Two) }, CheckSuite(Pass)),

                        CheckFuncDef("f", NoParameters, CheckSuite(Pass), returnAnnotation: One),

                        CheckFuncDef("f", new[] { CheckParameter("a", annotation: One) }, CheckSuite(Pass), returnAnnotation: One),

                        CheckFuncDef("f", new[] { CheckParameter("a", defaultValue: Two, annotation: One) }, CheckSuite(Pass)),
                        CheckFuncDef("f", new[] { CheckParameter(null, ParameterKind.List), CheckParameter("a", ParameterKind.KeywordOnly) }, CheckSuite(Pass))

                    )
                );
            }

            foreach (var version in V2Versions) {
                ParseErrors("FuncDefV3.py", version,
                    new ErrorResult("positional parameter after * args not allowed", new SourceSpan(1, 11, 1, 12)),
                    new ErrorResult("positional parameter after * args not allowed", new SourceSpan(2, 11, 2, 16)),
                    new ErrorResult("invalid syntax, parameter annotations require 3.x", new SourceSpan(4, 7, 4, 11)),
                    new ErrorResult("invalid syntax, parameter annotations require 3.x", new SourceSpan(5, 7, 5, 12)),
                    new ErrorResult("invalid syntax, parameter annotations require 3.x", new SourceSpan(6, 7, 6, 13)),
                    new ErrorResult("invalid syntax, parameter annotations require 3.x", new SourceSpan(7, 7, 7, 11)),
                    new ErrorResult("invalid syntax, parameter annotations require 3.x", new SourceSpan(7, 13, 7, 18)),
                    new ErrorResult("invalid syntax, parameter annotations require 3.x", new SourceSpan(7, 20, 7, 26)),
                    new ErrorResult("invalid syntax, return annotations require 3.x", new SourceSpan(9, 9, 9, 13)),
                    new ErrorResult("invalid syntax, parameter annotations require 3.x", new SourceSpan(11, 7, 11, 11)),
                    new ErrorResult("invalid syntax, return annotations require 3.x", new SourceSpan(11, 13, 11, 17)),
                    new ErrorResult("invalid syntax, parameter annotations require 3.x", new SourceSpan(13, 7, 13, 15)),
                    new ErrorResult("invalid syntax", new SourceSpan(15, 7, 15, 8))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void FuncDefV3Illegal() {
            foreach (var version in V3Versions) {
                ParseErrors("FuncDefV3Illegal.py", version,
                    new ErrorResult("named arguments must follow bare *", new SourceSpan(1, 7, 1, 8)),
                    new ErrorResult("named arguments must follow bare *", new SourceSpan(2, 7, 2, 8))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void FuncDefTrailingComma() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFileNoErrors("FuncDefTrailingComma.py", version),
                    CheckSuite(
                        CheckFuncDef("f", new[] { CheckParameter("a") }, CheckSuite(Pass)),
                        CheckFuncDef("f", new[] { CheckParameter("a"), CheckParameter("b", ParameterKind.List) }, CheckSuite(Pass)),
                        CheckFuncDef("f", new[] { CheckParameter("a"), CheckParameter("b", ParameterKind.List), CheckParameter("c", ParameterKind.Dictionary) }, CheckSuite(Pass)),
                        CheckFuncDef("f", new[] { CheckParameter("a", ParameterKind.List) }, CheckSuite(Pass)),
                        CheckFuncDef("f", new[] { CheckParameter("a", ParameterKind.Dictionary) }, CheckSuite(Pass))
                    )
                );
            }
        }

        [TestMethod, Priority(0)]
        public void CoroutineDef() {
            foreach (var version in V35AndUp) {
                CheckAst(
                    ParseFileNoErrors("CoroutineDef.py", version),
                    CheckSuite(
                        CheckCoroutineDef(CheckFuncDef("f", NoParameters, CheckSuite(
                            CheckAsyncForStmt(CheckForStmt(Fob, Oar, CheckSuite(Pass))),
                            CheckAsyncWithStmt(CheckWithStmt(Baz, CheckSuite(Pass)))
                        )))
                    )
                );
            }

            ParseErrors("CoroutineDefIllegal.py", PythonLanguageVersion.V35,
                new ErrorResult("'yield' inside async function", new SourceSpan(2, 5, 2, 10)),
                new ErrorResult("'yield' inside async function", new SourceSpan(3, 9, 3, 14)),
                new ErrorResult("unexpected token 'for'", new SourceSpan(6, 11, 6, 14)),
                new ErrorResult("unexpected token ':'", new SourceSpan(6, 25, 6, 26)),
                new ErrorResult("unexpected token '<newline>'", new SourceSpan(6, 26, 7, 9)),
                new ErrorResult("unexpected token '<indent>'", new SourceSpan(6, 26, 7, 9)),
                new ErrorResult("unexpected token '<dedent>'", new SourceSpan(8, 1, 9, 1)),
                new ErrorResult("unexpected token 'async'", new SourceSpan(9, 1, 9, 6)),
                new ErrorResult("unexpected token 'with'", new SourceSpan(13, 11, 13, 15)),
                new ErrorResult("unexpected token ':'", new SourceSpan(13, 19, 13, 20)),
                new ErrorResult("unexpected token '<newline>'", new SourceSpan(13, 20, 14, 9)),
                new ErrorResult("unexpected token '<indent>'", new SourceSpan(13, 20, 14, 9)),
                new ErrorResult("unexpected token '<dedent>'", new SourceSpan(15, 1, 16, 1)),
                new ErrorResult("unexpected token 'async'", new SourceSpan(16, 1, 16, 6))
            );

            foreach (var version in V36AndUp) {
                ParseErrors("CoroutineDefIllegal.py", version,
                    new ErrorResult("unexpected token 'for'", new SourceSpan(6, 11, 6, 14)),
                    new ErrorResult("illegal target for annotation", new SourceSpan(6, 15, 6, 26)),
                    new ErrorResult("unexpected indent", new SourceSpan(7, 9, 7, 13)),
                    new ErrorResult("unexpected token '<dedent>'", new SourceSpan(8, 1, 9, 1)),
                    new ErrorResult("unexpected token 'async'", new SourceSpan(9, 1, 9, 6)),
                    new ErrorResult("unexpected token 'with'", new SourceSpan(13, 11, 13, 15)),
                    new ErrorResult("unexpected token '<newline>'", new SourceSpan(13, 20, 14, 9)),
                    new ErrorResult("unexpected indent", new SourceSpan(14, 9, 14, 13)),
                    new ErrorResult("unexpected token '<dedent>'", new SourceSpan(15, 1, 16, 1)),
                    new ErrorResult("unexpected token 'async'", new SourceSpan(16, 1, 16, 6))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void ClassDef() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("ClassDef.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckClassDef("C", CheckSuite(Pass)),
                        CheckClassDef("C", CheckSuite(Pass), new[] { CheckArg("object") }),
                        CheckClassDef("C", CheckSuite(Pass), new[] { CheckArg("list"), CheckArg("object") }),
                        CheckClassDef("C",
                            CheckSuite(
                                CheckClassDef("_C__D",
                                    CheckSuite(
                                        CheckClassDef("_D__E",
                                            CheckSuite(Pass)
                                        )
                                    )
                                )
                            )
                        )
                    )
                );
            }
        }

        [TestMethod, Priority(0)]
        public void ClassDef3x() {
            foreach (var version in V3Versions) {
                CheckAst(
                    ParseFile("ClassDef3x.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckClassDef("C", CheckSuite(Pass), new[] { CheckNamedArg("metaclass", One) }),
                        CheckClassDef("C", CheckSuite(Pass), new[] { CheckArg("object"), CheckNamedArg("metaclass", One) }),
                        CheckClassDef("C", CheckSuite(Pass), new[] { CheckArg("list"), CheckArg("object"), CheckNamedArg("fob", One) })
                    )
                );
            }

            foreach (var version in V2Versions) {
                ParseErrors("ClassDef3x.py", version,
                    new ErrorResult("invalid syntax", new SourceSpan(1, 9, 1, 20)),
                    new ErrorResult("invalid syntax", new SourceSpan(2, 17, 2, 28)),
                    new ErrorResult("invalid syntax", new SourceSpan(3, 23, 3, 28))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void NamedExpressions() {
            foreach (var version in V38AndUp) {
                var errors = new CollectingErrorSink();
                CheckAst(
                    ParseFile("NamedExpressions.py", errors, version),
                    CheckSuite(
                        CheckExprStmt(
                            CheckParenExpr(
                                CheckNamedExpr(
                                    CheckNameExpr("a"),
                                    One
                                )
                            )
                        ), CheckExprStmt(
                            CheckListExpr(
                                CheckNamedExpr(
                                    CheckNameExpr("a"),
                                    One
                                ),
                                One
                            )
                        ),
                        CheckFuncDef("f", new[] { CheckParameter("x") },
                            CheckSuite(
                                CheckReturnStmt(
                                    One
                                )
                            )
                        ),
                        CheckExprStmt(
                            CheckListExpr(
                                CheckNamedExpr(
                                    CheckNameExpr("y"),
                                    CheckCallExpression(
                                        CheckNameExpr("f"),
                                        PositionalArg(One)
                                    )
                                ),
                                CheckBinaryExpression(
                                    CheckNameExpr("y"),
                                    PythonOperator.Power,
                                    Two
                                )
                            )
                        ),
                        CheckIfStmt(
                            IfTests(
                                IfTest(
                                    CheckBinaryExpression(
                                        CheckParenExpr(
                                            CheckNamedExpr(
                                                CheckNameExpr("match"),
                                                One
                                            )
                                        ),
                                        PythonOperator.IsNot,
                                        // None
                                        CheckConstant(
                                            null
                                        )
                                    ),
                                    CheckSuite(Pass)
                                )
                            )
                        ),
                        CheckWhileStmt(
                            CheckNamedExpr(
                                CheckNameExpr("chunk"),
                                CheckCallExpression(
                                    CheckNameExpr("f"),
                                    PositionalArg(One)
                                )
                            ),
                            CheckSuite(Pass)
                        ),
                        CheckFuncDef("foo", new[] {
                                CheckParameter("answer", ParameterKind.Normal, CheckConstant(5),
                                    CheckParenExpr(
                                        CheckNamedExpr(
                                            CheckNameExpr("p"),
                                            CheckConstant(42)
                                        )
                                    )
                                ), CheckParameter("cat", ParameterKind.Normal, CheckConstant(""))
                            },
                            CheckSuite(
                                CheckReturnStmt(
                                    One
                                )
                            )
                        ),
                        CheckLambdaStmt(
                            NoParameters,
                            CheckParenExpr(
                                CheckNamedExpr(
                                    CheckNameExpr("y"),
                                    One
                                )
                            )
                        ),
                        CheckAssignment(
                            CheckNameExpr("x"),
                            CheckParenExpr(
                                CheckNamedExpr(
                                    CheckNameExpr("y"),
                                    One
                                )
                            )
                        ),
                        CheckExprStmt(
                            CheckCallExpression(
                                CheckNameExpr("foo"),
                                new[] {
                                    PositionalArg(
                                        CheckNamedExpr(
                                            CheckNameExpr("x"),
                                            One
                                        )
                                    ),
                                    CheckNamedArg(
                                        "cat",
                                        CheckConstant("vector")
                                    )
                                }
                            )
                        ),
                        CheckExprStmt(
                            CheckParenExpr(
                                CheckNamedExpr(
                                    CheckNameExpr("a"),
                                    CheckAndExpression(
                                        One,
                                        None
                                    )
                                )
                            )
                        ),
                        CheckExprStmt(
                            CheckParenExpr(
                                CheckNamedExpr(
                                    CheckNameExpr("a"),
                                    CheckConditionalExpression(
                                        One,
                                        CheckConstant(false),
                                        Two
                                    )
                                )
                            )
                        ),
                        CheckExprStmt(
                            CheckParenExpr(
                                CheckNamedExpr(
                                    CheckParenExpr(
                                        CheckNameExpr("x")
                                    ),
                                    One
                                )
                            )
                        ),
                        CheckIfStmt(
                            IfTests(
                                IfTest(
                                    CheckNamedExpr(
                                        CheckNameExpr("x"),
                                        CheckNameExpr("a")
                                    ),
                                    CheckSuite(Pass)
                                )
                            )
                        ),
                        CheckClassDef(
                            "LambdaTop",
                            CheckSuite(
                                CheckExprStmt(
                                    CheckListComp(
                                        CheckParenExpr(
                                            CheckLambda(
                                                NoParameters,
                                                CheckParenExpr(
                                                    CheckNamedExpr(
                                                        CheckNameExpr("z"),
                                                        CheckNameExpr("x")
                                                    )
                                                )
                                            )
                                        ),
                                        CompFor(
                                            CheckNameExpr("x"),
                                            CheckCallExpression(CheckNameExpr("range"), PositionalArg(One))
                                        )
                                    )
                                )
                            )
                        ),
                        CheckExprStmt(
                            CheckListComp(
                                CheckParenExpr(
                                    CheckLambda(
                                        new[] { CheckParameter("x") },
                                        CheckParenExpr(
                                            CheckNamedExpr(
                                                CheckNameExpr("x"),
                                                CheckNameExpr("x")
                                            )
                                        )
                                    )
                                ),
                                CompFor(
                                    CheckNameExpr("x"),
                                    CheckCallExpression(CheckNameExpr("range"), PositionalArg(One))
                                )
                            )
                        )
                    )
                );
                errors.Errors.Should().BeEmpty();
            }
        }

        [TestMethod, Priority(0)]
        public void NamedExpressionsErrors() {
            foreach (var version in V38AndUp) {
                var errors = new CollectingErrorSink();
                ParseFile("NamedExpressionsErrors.py", errors, version);
                errors.Errors.Should().BeEquivalentTo(new[] {
                    new ErrorResult("Named expression must be parenthesized in this context", new SourceSpan(1, 3, 1, 5)),
                    new ErrorResult("Named expression must be parenthesized in this context", new SourceSpan(2, 11, 2, 13)),
                    new ErrorResult("Named expression must be parenthesized in this context", new SourceSpan(3, 7, 3, 9)),
                    new ErrorResult("Named expression must be parenthesized in this context", new SourceSpan(4, 19, 4, 21)),
                    new ErrorResult("Cannot use named assignment with subscript", new SourceSpan(8, 2, 8, 6)),
                    new ErrorResult("Cannot use named assignment with attribute", new SourceSpan(9, 2, 9, 5)),
                    new ErrorResult("Named expression must be parenthesized in this context", new SourceSpan(12, 9, 12, 11)),
                    new ErrorResult("Named expression must be parenthesized in this context", new SourceSpan(14, 21, 14, 23)),
                    new ErrorResult("Named expression must be parenthesized in this context", new SourceSpan(17, 9, 17, 11)),
                });
            }
        }

        [TestMethod, Priority(0)]
        public void NamedExpressionScopeErrors() {
            foreach (var version in V38AndUp) {
                var errors = new CollectingErrorSink();
                ParseFile("NamedExpressionScopeErrors.py", errors, version);
                errors.Errors.Should().BeEquivalentTo(new[] {
                    new ErrorResult("assignment expression cannot be used in a comprehension iterable expression", new SourceSpan(1, 17, 1, 32)),
                    new ErrorResult("assignment expression cannot be used in a comprehension iterable expression", new SourceSpan(2, 27, 2, 42)),
                    new ErrorResult("assignment expression cannot be used in a comprehension iterable expression", new SourceSpan(3, 20, 3, 35)),
                    new ErrorResult("assignment expression cannot be used in a comprehension iterable expression", new SourceSpan(4, 17, 4, 32)),
                    new ErrorResult("assignment expression cannot be used in a comprehension iterable expression", new SourceSpan(5, 33, 5, 48)),
                    new ErrorResult("assignment expression cannot rebind comprehension iteration variable 'j'", new SourceSpan(7, 4, 7, 5)),
                    new ErrorResult("assignment expression cannot rebind comprehension iteration variable 'i'", new SourceSpan(8, 2, 8, 3)),
                    new ErrorResult("assignment expression cannot be used in a comprehension iterable expression", new SourceSpan(9, 16, 9, 26)),
                    new ErrorResult("assignment expression cannot rebind comprehension iteration variable 'i'", new SourceSpan(11, 13, 11, 14)),
                    new ErrorResult("assignment expression cannot rebind comprehension iteration variable 'j'", new SourceSpan(12, 34, 12, 35)),
                    new ErrorResult("assignment expression cannot be used in a comprehension iterable expression", new SourceSpan(14, 16, 14, 26)),
                    new ErrorResult("assignment expression cannot be used in a comprehension iterable expression", new SourceSpan(15, 34, 15, 44)),
                    new ErrorResult("assignment expression cannot be used in a comprehension iterable expression", new SourceSpan(16, 28, 16, 38)),
                    new ErrorResult("assignment expression cannot be used in a comprehension iterable expression", new SourceSpan(17, 25, 17, 35)),
                    new ErrorResult("assignment expression within a comprehension cannot be used in a class body", new SourceSpan(20, 7, 20, 13)),
                    new ErrorResult("comprehension inner loop cannot rebind assignment expression target 'j'", new SourceSpan(22, 43, 22, 44)),
                });
            }
        }

        [TestMethod, Priority(0)]
        public void AssignStmt() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("AssignStmt.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckAssignment(CheckIndexExpression(Fob, One), Two),
                        CheckAssignment(CheckMemberExpr(Fob, "oar"), One),
                        CheckAssignment(Fob, One),
                        CheckAssignment(CheckParenExpr(Fob), One),
                        CheckAssignment(CheckTupleExpr(Fob, Oar), CheckTupleExpr(One, Two)),
                        CheckAssignment(CheckTupleExpr(Fob, Oar), CheckTupleExpr(One, Two)),
                        CheckAssignment(CheckTupleExpr(Fob, Oar), Baz),
                        CheckAssignment(CheckListExpr(Fob, Oar), CheckTupleExpr(One, Two)),
                        CheckAssignment(CheckListExpr(Fob, Oar), Baz),
                        CheckAssignment(new[] { Fob, Oar }, Baz)
                    )
                );
            }
        }

        [TestMethod, Priority(0)]
        public void AssignStmt2x() {
            foreach (var version in V2Versions) {
                var sink = new CollectingErrorSink();
                CheckAst(
                    ParseFile("AssignStmt2x.py", sink, version),
                    CheckSuite(
                        CheckAssignment(Fob, CheckUnaryExpression(PythonOperator.Negate, CheckBinaryExpression(CheckConstant((BigInteger)2), PythonOperator.Power, CheckConstant(31))))
                    )
                );
                sink.Errors.Should().BeEmpty();
            }
        }

        [TestMethod, Priority(0)]
        public void AssignStmt25() {
            foreach (var version in V26AndUp) {
                CheckAst(
                    ParseFile("AssignStmt25.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckFuncDef(
                            "f",
                            NoParameters,
                            CheckSuite(
                                CheckAssignment(Fob, CheckYieldExpr(One)),
                                CheckAssignment(Fob, PythonOperator.Add, CheckYieldExpr(One))
                            )
                        )
                    )
                );
            }

            ParseErrors("AssignStmt25.py", PythonLanguageVersion.V24,
                new ErrorResult("unexpected token 'yield'", new SourceSpan(2, 11, 2, 16)),
                new ErrorResult("invalid syntax", new SourceSpan(2, 17, 2, 18)),
                new ErrorResult("unexpected token 'yield'", new SourceSpan(3, 12, 3, 17)),
                new ErrorResult("invalid syntax", new SourceSpan(3, 18, 3, 19))
            );
        }

        [TestMethod, Priority(0)]
        public void AssignStmtV3() {
            foreach (var version in V3Versions) {
                CheckAst(
                    ParseFile("AssignStmtV3.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckAssignment(CheckTupleExpr(CheckStarExpr(Fob), Oar, Baz), CheckTupleExpr(One, Two, Three, Four)),
                        CheckAssignment(CheckTupleExpr(Fob, CheckStarExpr(Oar), Baz), CheckTupleExpr(One, Two, Three, Four)),
                        CheckAssignment(CheckListExpr(Fob, CheckStarExpr(Oar), Baz), CheckTupleExpr(One, Two, Three, Four)),
                        CheckAssignment(CheckListExpr(CheckStarExpr(Fob), Oar, Baz), CheckTupleExpr(One, Two, Three, Four))
                    )
                );
            }

            foreach (var version in V2Versions) {
                ParseErrors("AssignStmtV3.py", version,
                    new ErrorResult("invalid syntax", new SourceSpan(1, 2, 1, 5)),
                    new ErrorResult("invalid syntax", new SourceSpan(2, 7, 2, 10)),
                    new ErrorResult("invalid syntax", new SourceSpan(3, 8, 3, 11)),
                    new ErrorResult("invalid syntax", new SourceSpan(4, 3, 4, 6))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void AssignStmtIllegalV3() {
            foreach (var version in V3Versions) {
                CheckAst(
                    ParseFile("AssignStmtIllegalV3.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckAssignment(CheckTupleExpr(Fob, CheckStarExpr(Oar), CheckStarExpr(Baz)), CheckTupleExpr(One, Two, Three, Four))
                    )
                );
            }

            foreach (var version in V2Versions) {
                ParseErrors("AssignStmtIllegalV3.py", version,
                    new ErrorResult("invalid syntax", new SourceSpan(1, 7, 1, 10)),
                    new ErrorResult("invalid syntax", new SourceSpan(1, 13, 1, 16))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void AssignStmtIllegal() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("AssignStmtIllegal.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckAssignment(CheckBinaryExpression(Fob, PythonOperator.Add, Oar), One),
                        CheckAssignment(CheckCallExpression(Fob), One),
                        CheckAssignment(None, One),
                        CheckAssignment(Two, One),
                        CheckAssignment(CheckGeneratorComp(Fob, CompFor(Fob, Oar)), One),
                        CheckAssignment(CheckTupleExpr(Fob, Oar), PythonOperator.Add, One),
                        CheckFuncDef("f", NoParameters,
                            CheckSuite(
                                CheckAssignment(CheckParenExpr(CheckYieldExpr(Fob)), One)
                            )
                        )
                    )
                );
            }

            foreach (var version in AllVersions) {
                ParseErrors("AssignStmtIllegal.py", version,
                    new ErrorResult("can't assign to binary operator", new SourceSpan(1, 1, 1, 10)),
                    new ErrorResult("can't assign to function call", new SourceSpan(2, 1, 2, 6)),
                    new ErrorResult("assignment to None", new SourceSpan(3, 1, 3, 5)),
                    new ErrorResult("can't assign to literal", new SourceSpan(4, 1, 4, 2)),
                    new ErrorResult("can't assign to generator expression", new SourceSpan(5, 1, 5, 21)),
                    new ErrorResult("illegal expression for augmented assignment", new SourceSpan(6, 1, 6, 9)),
                    new ErrorResult("can't assign to yield expression", new SourceSpan(8, 5, 8, 16))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void AssignToNamedExprIllegal() {
            foreach (var version in V38AndUp) {
                var errors = new CollectingErrorSink();
                ParseString("(a := 1) = 1", errors, version);
                errors.Errors.Should().BeEquivalentTo(new[]{
                    new ErrorResult("can't assign to named expression", new SourceSpan(1, 1, 1, 9))
                });
            }
        }

        [TestMethod, Priority(0)]
        public void AwaitStmt() {
            var AwaitFob = CheckAwaitExpression(Fob);
            foreach (var version in V35AndUp) {
                var sink = new CollectingErrorSink();
                CheckAst(
                    ParseFile("AwaitStmt.py", sink, version),
                    CheckSuite(CheckCoroutineDef(CheckFuncDef("quox", NoParameters, CheckSuite(
                        CheckExprStmt(AwaitFob),
                        CheckExprStmt(CheckAwaitExpression(CheckCallExpression(Fob))),
                        CheckExprStmt(CheckCallExpression(CheckParenExpr(AwaitFob))),
                        CheckBinaryStmt(One, PythonOperator.Add, AwaitFob),
                        CheckBinaryStmt(One, PythonOperator.Power, AwaitFob),
                        CheckBinaryStmt(One, PythonOperator.Power, CheckUnaryExpression(PythonOperator.Negate, AwaitFob))
                    ))))
                );
                sink.Errors.Should().BeEmpty();
            }
        }

        [TestMethod, Priority(0)]
        public void AwaitStmtPreV35() {
            foreach (var version in AllVersions.Except(V35AndUp)) {
                ParseErrors("AwaitStmt.py", version,
                    new ErrorResult("unexpected token 'def'", new SourceSpan(1, 7, 1, 10)),
                    new ErrorResult("unexpected token ':'", new SourceSpan(1, 17, 1, 18)),
                    new ErrorResult("unexpected indent", new SourceSpan(2, 5, 2, 10)),
                    new ErrorResult("unexpected token 'fob'", new SourceSpan(2, 11, 2, 14)),
                    new ErrorResult("unexpected token 'fob'", new SourceSpan(3, 11, 3, 14)),
                    new ErrorResult("unexpected token 'fob'", new SourceSpan(4, 12, 4, 15)),
                    new ErrorResult("unexpected token 'fob'", new SourceSpan(4, 12, 4, 15)),
                    new ErrorResult("unexpected token ')'", new SourceSpan(4, 15, 4, 16)),
                    new ErrorResult("unexpected token '('", new SourceSpan(4, 16, 4, 17)),
                    new ErrorResult("unexpected token ')'", new SourceSpan(4, 17, 4, 18)),
                    new ErrorResult("unexpected token 'fob'", new SourceSpan(5, 15, 5, 18)),
                    new ErrorResult("unexpected token 'fob'", new SourceSpan(6, 16, 6, 19)),
                    new ErrorResult("unexpected token 'fob'", new SourceSpan(7, 17, 7, 20)),
                    new ErrorResult("unexpected token '<dedent>'", new SourceSpan(7, 20, 8, 1))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void AwaitAsyncNames() {
            var Async = CheckNameExpr("async");
            var Await = CheckNameExpr("await");
            foreach (var version in V35AndUp) {
                var ast = ParseFileNoErrors("AwaitAsyncNames.py", version);
                CheckAst(
                    ast,
                    CheckSuite(
                        CheckExprStmt(Async),
                        CheckExprStmt(Await),
                        CheckAssignment(Async, Fob),
                        CheckAssignment(Await, Fob),
                        CheckAssignment(Fob, Async),
                        CheckAssignment(Fob, Await),
                        CheckFuncDef("async", NoParameters, CheckSuite(Pass)),
                        CheckFuncDef("await", NoParameters, CheckSuite(Pass)),
                        CheckClassDef("async", CheckSuite(Pass)),
                        CheckClassDef("await", CheckSuite(Pass)),
                        CheckCallStmt(Async, CheckArg("fob")),
                        CheckCallStmt(Await, CheckArg("fob")),
                        CheckCallStmt(Fob, CheckArg("async")),
                        CheckCallStmt(Fob, CheckArg("await")),
                        CheckMemberStmt(Fob, "async"),
                        CheckMemberStmt(Fob, "await"),
                        CheckFuncDef("fob", new[] { CheckParameter("async"), CheckParameter("await") }, CheckSuite(Pass))
                    )
                );
                ParseFileNoErrors("AwaitAsyncNames.py", version);
            }
        }

        [TestMethod, Priority(0)]
        public void AwaitStmtIllegal() {
            //foreach (var version in V35AndUp) {
            //    CheckAst(
            //        ParseFile("AwaitStmtIllegal.py", ErrorSink.Null, version),
            //        CheckSuite(
            //        )
            //    );
            //}

            foreach (var version in V35AndUp) {
                ParseErrors("AwaitStmtIllegal.py", version,
                    new ErrorResult("invalid syntax", new SourceSpan(1, 7, 1, 11)),
                    new ErrorResult("unexpected token 'fob'", new SourceSpan(4, 11, 4, 14)),
                    new ErrorResult("unexpected token '<newline>'", new SourceSpan(4, 14, 5, 1)),
                    new ErrorResult("unexpected token '<NL>'", new SourceSpan(5, 1, 6, 1)),
                    new ErrorResult("unexpected token 'fob'", new SourceSpan(7, 11, 7, 14)),
                    new ErrorResult("unexpected token '<newline>'", new SourceSpan(7, 14, 8, 1)),
                    new ErrorResult("unexpected token '<NL>'", new SourceSpan(8, 1, 9, 1))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void AsyncForComprehension() {
            foreach (var version in V37AndUp) {
                var ast = ParseFileNoErrors("AsyncFor37.py", version);
                CheckAst(
                    ast,
                    CheckSuite(
                        CheckFuncDef("test1", NoParameters, CheckSuite(
                            CheckReturnStmt(CheckGeneratorComp(Fob, AsyncCompFor(Fob, CheckListExpr())))
                        )),
                        CheckFuncDef("test2", NoParameters, CheckSuite(
                            CheckReturnStmt(CheckGeneratorComp(Fob, CompFor(Fob, CheckListExpr()), CompIf(CheckAwaitExpression(Fob))))
                        ))
                    )
                );
            }

            foreach (var version in V36AndUp) {
                var ast = ParseFileNoErrors("AsyncFor.py", version);
                CheckAst(
                    ast,
                    CheckSuite(CheckCoroutineDef(CheckFuncDef("f", NoParameters, CheckSuite(
                        CheckExprStmt(CheckListComp(Fob, AsyncCompFor(Fob, Oar))),
                        CheckExprStmt(CheckGeneratorComp(Fob, AsyncCompFor(Fob, Oar))),
                        CheckExprStmt(CheckSetComp(Fob, AsyncCompFor(Fob, Oar))),
                        CheckExprStmt(CheckDictComp(Fob, Fob, AsyncCompFor(Fob, Oar))),
                        CheckExprStmt(CheckListComp(Fob, CompFor(Fob, Oar), AsyncCompFor(Oar, Baz))),
                        CheckExprStmt(CheckGeneratorComp(Fob, CompFor(Fob, Oar), AsyncCompFor(Oar, Baz))),
                        CheckExprStmt(CheckListComp(CheckAwaitExpression(Fob), AsyncCompFor(Fob, Oar)))
                    ))))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void ConditionalExpr() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFile("ConditionalExpr.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckExprStmt(CheckConditionalExpression(One, Two, Three)),
                        CheckExprStmt(CheckConditionalExpression(One, Two, Three)),
                        CheckExprStmt(CheckConditionalExpression(One, Two, Three)),
                        CheckExprStmt(CheckConditionalExpression(CheckConstant(1.0), CheckConstant(2e10), Three)),
                        CheckExprStmt(CheckConditionalExpression(One, CheckConstant(2.0), Three))
                    )
                );
            }
        }

        [TestMethod, Priority(0)]
        public void ExecStmt() {
            foreach (var version in V2Versions) {
                CheckAst(
                    ParseFile("ExecStmt.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckExecStmt(Fob),
                        CheckExecStmt(Fob, Oar),
                        CheckExecStmt(Fob, Oar, Baz)
                    )
                );
            }

            foreach (var version in V3Versions) {
                ParseErrors("ExecStmt.py", version,
                    new ErrorResult("unexpected token 'fob'", new SourceSpan(1, 6, 1, 9)),
                    new ErrorResult("unexpected token 'fob'", new SourceSpan(2, 6, 2, 9)),
                    new ErrorResult("unexpected token 'in'", new SourceSpan(2, 10, 2, 12)),
                    new ErrorResult("unexpected token 'oar'", new SourceSpan(2, 13, 2, 16)),
                    new ErrorResult("unexpected token 'fob'", new SourceSpan(3, 6, 3, 9)),
                    new ErrorResult("unexpected token 'in'", new SourceSpan(3, 10, 3, 12)),
                    new ErrorResult("unexpected token 'oar'", new SourceSpan(3, 13, 3, 16)),
                    new ErrorResult("unexpected token ','", new SourceSpan(3, 16, 3, 17)),
                    new ErrorResult("unexpected token 'baz'", new SourceSpan(3, 18, 3, 21))
                );
            }

        }

        [TestMethod, Priority(0)]
        public void EllipsisExpr() {
            foreach (var version in V3Versions) {
                CheckAst(
                    ParseFile("Ellipsis.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckCallStmt(Fob, PositionalArg(Ellipsis)),
                        CheckBinaryStmt(One, PythonOperator.Add, Ellipsis)
                    )
                );
            }

            foreach (var version in V2Versions) {
                ParseErrors("Ellipsis.py", version,
                    new ErrorResult("unexpected token '.'", new SourceSpan(1, 5, 1, 6)),
                    new ErrorResult("syntax error", new SourceSpan(1, 7, 1, 8)),
                    new ErrorResult("syntax error", new SourceSpan(1, 8, 1, 9)),
                    new ErrorResult("unexpected token '.'", new SourceSpan(2, 5, 2, 6)),
                    new ErrorResult("syntax error", new SourceSpan(2, 7, 2, 8)),
                    new ErrorResult("syntax error", new SourceSpan(2, 8, 3, 1))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void IncompleteMemberExpr() {
            foreach (var version in V2Versions) {
                ParseErrors("IncompleteMemberExpr.py", version,
                    new ErrorResult("syntax error", new SourceSpan(1, 3, 1, 4)),
                    new ErrorResult("syntax error", new SourceSpan(3, 3, 4, 1)),
                    new ErrorResult("syntax error", new SourceSpan(5, 3, 5, 3))
                );
            }
        }

        [TestMethod, Priority(0)]
        public void FromFuture() {
            foreach (var version in AllVersions) {
                CheckAst(
                    ParseFileNoErrors("FromFuture24.py", version),
                    CheckSuite(
                        CheckFromImport("__future__", new[] { "division" }),
                        CheckFromImport("__future__", new[] { "generators" })
                    )
                );

                if (version == PythonLanguageVersion.V24) {
                    ParseErrors("FromFuture25.py", version,
                        new ErrorResult("future feature is not defined: with_statement", new SourceSpan(1, 1, 1, 38)),
                        new ErrorResult("future feature is not defined: absolute_import", new SourceSpan(2, 1, 2, 39))
                    );
                } else {
                    CheckAst(
                        ParseFileNoErrors("FromFuture25.py", version),
                        CheckSuite(
                            CheckFromImport("__future__", new[] { "with_statement" }),
                            CheckFromImport("__future__", new[] { "absolute_import" })
                        )
                    );
                }

                if (version == PythonLanguageVersion.V24 || version == PythonLanguageVersion.V25) {
                    ParseErrors("FromFuture26.py", version,
                        new ErrorResult("future feature is not defined: print_function", new SourceSpan(1, 1, 1, 38)),
                        new ErrorResult("future feature is not defined: unicode_literals", new SourceSpan(2, 1, 2, 40))
                    );
                } else {
                    CheckAst(
                        ParseFileNoErrors("FromFuture26.py", version),
                        CheckSuite(
                            CheckFromImport("__future__", new[] { "print_function" }),
                            CheckFromImport("__future__", new[] { "unicode_literals" })
                        )
                    );
                }

                if (version < PythonLanguageVersion.V35) {
                    ParseErrors("FromFuture35.py", version,
                        new ErrorResult("future feature is not defined: generator_stop", new SourceSpan(1, 1, 1, 38))
                    );
                } else {
                    CheckAst(
                        ParseFileNoErrors("FromFuture35.py", version),
                        CheckSuite(
                            CheckFromImport("__future__", new[] { "generator_stop" })
                        )
                    );
                }
            }
        }

        [TestMethod, Priority(0)]
        public void VariableAnnotation() {
            Action<Expression> FobWithOar = e => {
                Assert.IsInstanceOfType(e, typeof(ExpressionWithAnnotation));
                Fob(((ExpressionWithAnnotation)e).Expression);
                Oar(((ExpressionWithAnnotation)e).Annotation);
            };
            Action<Expression> Fob1WithOar = e => {
                Assert.IsInstanceOfType(e, typeof(ExpressionWithAnnotation));
                CheckIndexExpression(Fob, One)(((ExpressionWithAnnotation)e).Expression);
                Oar(((ExpressionWithAnnotation)e).Annotation);
            };
            Action<Expression> FobOarWithBaz = e => {
                Assert.IsInstanceOfType(e, typeof(ExpressionWithAnnotation));
                CheckMemberExpr(Fob, "oar")(((ExpressionWithAnnotation)e).Expression);
                Baz(((ExpressionWithAnnotation)e).Annotation);
            };

            foreach (var version in V36AndUp) {
                CheckAst(
                    ParseFile("VarAnnotation.py", ErrorSink.Null, version),
                    CheckSuite(
                        CheckExprStmt(FobWithOar),
                        CheckAssignment(FobWithOar, One),
                        CheckExprStmt(Fob1WithOar),
                        CheckExprStmt(FobOarWithBaz),
                        CheckClassDef("C", CheckSuite(
                            CheckExprStmt(FobWithOar),
                            CheckAssignment(FobWithOar, One),
                            CheckExprStmt(Fob1WithOar),
                            CheckExprStmt(FobOarWithBaz)
                        )),
                        CheckFuncDef("f", null, CheckSuite(
                            CheckExprStmt(FobWithOar),
                            CheckAssignment(FobWithOar, One),
                            CheckExprStmt(Fob1WithOar),
                            CheckExprStmt(FobOarWithBaz)
                        ))
                    )
                );

                ParseErrors("VarAnnotationIllegal.py", version,
                    new ErrorResult("only single target (not tuple) can be annotated", new SourceSpan(1, 1, 1, 14)),
                    new ErrorResult("unexpected token ','", new SourceSpan(2, 9, 2, 10)),
                    new ErrorResult("only single target (not tuple) can be annotated", new SourceSpan(3, 1, 3, 18)),
                    new ErrorResult("unexpected token ','", new SourceSpan(4, 9, 4, 10)),
                    new ErrorResult("invalid syntax", new SourceSpan(5, 16, 5, 17))
               );
            }
        }

        //[TestMethod, Priority(2), Timeout(10 * 60 * 1000)]
        //[TestCategory("10s"), TestCategory("60s")]
        //public async Task StdLib() {
        //    var tasks = new List<KeyValuePair<string, Task<string>>>();

        //    foreach (var curVersion in PythonPaths.Versions) {
        //        Console.WriteLine("Starting: {0}", curVersion);
        //        tasks.Add(new KeyValuePair<string, Task<string>>(
        //            curVersion.ToString(),
        //            Task.Run(() => StdLibWorker(curVersion.Configuration))
        //        ));
        //    }

        //    Console.WriteLine("Started {0} tests", tasks.Count);
        //    Console.WriteLine(new string('=', 80));

        //    var anyErrors = false;
        //    foreach (var task in tasks) {
        //        string errors = null;
        //        try {
        //            errors = await task.Value;
        //        } catch (Exception ex) {
        //            errors = ex.ToString();
        //        }

        //        if (string.IsNullOrEmpty(errors)) {
        //            Console.WriteLine("{0} passed", task.Key);
        //        } else {
        //            Console.WriteLine("{0} errors:", task.Key);
        //            Console.WriteLine(errors);
        //            anyErrors = true;
        //        }
        //        Console.WriteLine(new string('=', 80));
        //    }

        //    Assert.IsFalse(anyErrors, "Errors occurred. See output trace for details.");
        //}

        [TestMethod, Priority(0)]
        public void ReportsErrorsUsingLocationOffset() {
            foreach (var version in AllVersions) {
                var errorSink = new CollectingErrorSink();
                var code = @"f = pass
f = pass";
                using (var reader = new StringReader(code)) {
                    var parser = Parser.CreateParser(reader, version, new ParserOptions() {
                        ErrorSink = errorSink,
                        InitialSourceLocation = new SourceLocation(0, 10, 10)
                    });
                    parser.ParseFile();
                }
                errorSink.Errors.Should().BeEquivalentTo(new[] {
                    new ErrorResult("unexpected token 'pass'", new SourceSpan(10, 14, 10, 18)),
                    new ErrorResult("unexpected token 'pass'", new SourceSpan(11, 5, 11, 9))
                });
            }
        }

        private static string StdLibWorker(InterpreterConfiguration configuration) {
            var files = new List<string>();
            var version = configuration.Version.ToLanguageVersion();

            CollectFiles(configuration.LibraryPath, files, new[] { "site-packages" });

            var skippedFiles = new HashSet<string>(new[] {
                    "py3_test_grammar.py",  // included in 2x distributions but includes 3x grammar
                    "py2_test_grammar.py",  // included in 3x distributions but includes 2x grammar
                    "proxy_base.py",        // included in Qt port to Py3k but installed in 2.x distributions
                    "test_pep3131.py"       // we need to update to support this.
                });
            var errorSink = new CollectingErrorSink();
            var errors = new Dictionary<string, List<ErrorResult>>();
            foreach (var file in files) {
                var filename = Path.GetFileName(file);
                if (skippedFiles.Contains(filename) || filename.StartsWith("badsyntax_") || filename.StartsWith("bad_coding") || file.IndexOf("\\lib2to3\\tests\\") != -1) {
                    continue;
                }

                switch (version) {
                    case PythonLanguageVersion.V36:
                        if (// https://github.com/Microsoft/PTVS/issues/1637
                            filename.Equals("test_unicode_identifiers.py", StringComparison.OrdinalIgnoreCase)
                            ) {
                            continue;
                        }
                        break;
                }

                var parser = Parser.CreateParser(new StreamReader(file), version, new ParserOptions() { ErrorSink = errorSink });
                var ast = parser.ParseFile();

                if (errorSink.Errors.Count != 0) {
                    var fileErrors = errorSink.Errors.ToList();
                    if (version == PythonLanguageVersion.V35) {
                        // TODO: https://github.com/Microsoft/PTVS/issues/337
                        fileErrors.RemoveAll(e => {
                            return e.Message == "non-keyword arg after keyword arg";
                        });
                    }

                    if (fileErrors.Any()) {
                        errors["\"" + file + "\""] = fileErrors;
                        errorSink = new CollectingErrorSink();
                    }
                }
            }

            if (errors.Count != 0) {
                var errorList = new StringBuilder();
                foreach (var keyValue in errors) {
                    errorList.Append(keyValue.Key + " :" + Environment.NewLine);
                    foreach (var error in keyValue.Value) {
                        errorList.AppendFormat("     {0} {1}{2}", error.Span, error.Message, Environment.NewLine);
                    }

                }
                return errorList.ToString();
            }
            return null;
        }

        [TestMethod, Priority(0)]
        public void SourceLocationTests() {
            Assert.AreEqual(0, new SourceLocation().Index);
            Assert.AreEqual(100, new SourceLocation(100, 1, 1).Index);
            try {
                var i = new SourceLocation(1, 1).Index;
                Assert.Fail("Expected InvalidOperationException");
            } catch (InvalidOperationException) {
            }

            var x = new SourceLocation(100, 5, 10);
            var y = x.AddColumns(int.MaxValue);
            Assert.AreEqual(int.MaxValue, y.Column);
            Assert.AreEqual(int.MaxValue, y.Index);

            y = x.AddColumns(int.MaxValue - 9);
            Assert.AreEqual(int.MaxValue, y.Column);
            Assert.AreEqual(int.MaxValue, y.Index);

            y = x.AddColumns(-5);
            Assert.AreEqual(5, y.Column);
            Assert.AreEqual(95, y.Index);

            y = x.AddColumns(-10);
            Assert.AreEqual(1, y.Column);
            Assert.AreEqual(91, y.Index);

            y = x.AddColumns(-100);
            Assert.AreEqual(1, y.Column);
            Assert.AreEqual(91, y.Index);

            y = x.AddColumns(int.MinValue);
            Assert.AreEqual(1, y.Column);
            Assert.AreEqual(91, y.Index);
        }

        [TestMethod, Priority(0)]
        public void FindArgument() {
            var AssertArg = ParseCall("f( a ,   b, c,d,*  x   , )");
            AssertArg(0, null);
            AssertArg(2, 0);
            AssertArg(5, 0);
            AssertArg(6, 1);
            AssertArg(10, 1);
            AssertArg(11, 2);
            AssertArg(13, 2);
            AssertArg(14, 3);
            AssertArg(15, 3);
            AssertArg(16, 4);
            AssertArg(23, 4);
            AssertArg(24, -1);
            AssertArg(25, -1);
            AssertArg(26, null);

            AssertArg = ParseCall("f(");
            AssertArg(0, null);
            AssertArg(1, null);
            AssertArg(2, 0);
        }

        private static Action<int, int?> ParseCall(string code) {
            var parser = Parser.CreateParser(new StringReader(code), PythonLanguageVersion.V36, new ParserOptions { Verbatim = true });
            var tree = parser.ParseTopExpression(null);
            if (Statement.GetExpression(tree.Body) is CallExpression ce) {
                return (index, expected) => {
                    var actual = ce.GetArgumentAtIndex(tree, index, out var i) ? i : (int?)null;
                    Assert.AreEqual(expected, actual);
                };
            }
            Assert.Fail($"Unexpected expression {tree}");
            return null;
        }

        [TestMethod, Priority(0)]
        public void CommentLocations() {
            var parser = Parser.CreateParser(new StringReader(@"# line 1

# line 3
pass
  # line 4"), PythonLanguageVersion.V36);
            var tree = parser.ParseFile();

            tree.CommentLocations.Should().Equal(
                new SourceLocation(1, 1),
                new SourceLocation(3, 1),
                new SourceLocation(5, 3)
            );

            parser = Parser.CreateParser(new StringReader(@"# line 1
"), PythonLanguageVersion.V36);
            var tree1 = parser.ParseFile();
            parser = Parser.CreateParser(new StringReader(@"# line 3
pass
  # line 4"), PythonLanguageVersion.V36);
            tree = new PythonAst(new[] { tree1, parser.ParseFile() });
            tree.CommentLocations.Should().Equal(
                new SourceLocation(1, 1),
                new SourceLocation(3, 1),
                new SourceLocation(5, 3)
            );
        }

        #endregion

        #region Checker Factories / Helpers

        private void ParseErrors(string filename, PythonLanguageVersion version, params ErrorResult[] errors) {
            ParseErrors(filename, version, Severity.Hint, errors);
        }

        private void ParseErrors(string filename, PythonLanguageVersion version, Severity indentationInconsistencySeverity, params ErrorResult[] errors) {
            ParseErrors(filename, version, new ParserOptions() {
                IndentationInconsistencySeverity = indentationInconsistencySeverity,
            }, errors);
        }

        private void ParseErrors(string filename, PythonLanguageVersion version, ParserOptions options, params ErrorResult[] errors) {
            var sink = new CollectingErrorSink();
            options.ErrorSink = sink;
            ParseFile(filename, version, options);

            var foundErrors = new StringBuilder();
            for (var i = 0; i < sink.Errors.Count; i++) {
                foundErrors.AppendFormat("{0}{1}{2}",
                    sink.Errors[i].Format(),
                    i == sink.Errors.Count - 1 ? string.Empty : ",",
                    Environment.NewLine
                );
            }

            var finalErrors = foundErrors.ToString();
            Console.WriteLine(finalErrors);

            sink.Errors.ToArray().Should().HaveErrors(errors);
        }

        private static PythonAst ParseFileNoErrors(string filename, PythonLanguageVersion version, Severity indentationInconsistencySeverity = Severity.Hint) {
            var errorSink = new CollectingErrorSink();
            var ast = ParseFile(filename, errorSink, version, indentationInconsistencySeverity);
            foreach (var warn in errorSink.Warnings) {
                Trace.TraceInformation("WARN: {0} {1}", warn.Span, warn.Message);
            }
            foreach (var err in errorSink.Errors) {
                Trace.TraceInformation("ERR:  {0} {1}", err.Span, err.Message);
            }
            Assert.AreEqual(0, errorSink.Warnings.Count + errorSink.Errors.Count, "Parse errors occurred");
            return ast;
        }

        private static PythonAst ParseFile(string filename, ErrorSink errorSink, PythonLanguageVersion version, Severity indentationInconsistencySeverity = Severity.Hint) {
            return ParseFile(filename, version, new ParserOptions() {
                ErrorSink = errorSink,
                IndentationInconsistencySeverity = indentationInconsistencySeverity,
            });
        }

        private static PythonAst ParseFile(string filename, PythonLanguageVersion version, ParserOptions options) {
            var src = TestData.GetPath("TestData", "Grammar", filename);
            using (var reader = new StreamReader(src, true)) {
                var parser = Parser.CreateParser(reader, version, options);
                return parser.ParseFile();
            }
        }

        private static PythonAst ParseString(string content, ErrorSink errorSink, PythonLanguageVersion version, Severity indentationInconsistencySeverity = Severity.Hint) {
            using (var reader = new StringReader(content)) {
                var parser = Parser.CreateParser(reader, version, new ParserOptions() { ErrorSink = errorSink, IndentationInconsistencySeverity = indentationInconsistencySeverity });
                return parser.ParseFile();
            }
        }

        private void CheckAst(PythonAst ast, Action<Statement> checkBody) {
            checkBody(ast.Body);
        }

        private static readonly Action<Expression> Zero = CheckConstant(0);
        private static readonly Action<Expression> One = CheckConstant(1);
        private static readonly Action<Expression> Two = CheckConstant(2);
        private static readonly Action<Expression> Three = CheckConstant(3);
        private static readonly Action<Expression> Four = CheckConstant(4);
        private static readonly Action<Expression> None = CheckConstant(null);
        private static readonly Action<Expression> Fob = CheckNameExpr("fob");
        private static readonly Action<Expression> Ellipsis = CheckConstant(Microsoft.Python.Parsing.Ellipsis.Value);
        private static readonly Action<Expression> Oar = CheckNameExpr("oar");
        private static readonly Action<Expression> Baz = CheckNameExpr("baz");
        private static readonly Action<Expression> Quox = CheckNameExpr("quox");
        private static readonly Action<Expression> Exception = CheckNameExpr("Exception");
        private static readonly Action<Statement> Pass = CheckEmptyStmt();
        private static readonly Action<Statement> Break = CheckBreakStmt();
        private static readonly Action<Statement> Continue = CheckContinueStmt();


        private static Action<Statement> CheckSuite(params Action<Statement>[] statements) {
            return stmt => {
                Assert.AreEqual(typeof(SuiteStatement), stmt.GetType());
                var suite = (SuiteStatement)stmt;
                Assert.AreEqual(statements.Length, suite.Statements.Count);
                for (var i = 0; i < suite.Statements.Count; i++) {
                    try {
                        statements[i](suite.Statements[i]);
                    } catch (AssertFailedException e) {
                        Trace.TraceError(e.ToString());
                        throw new AssertFailedException(string.Format("Suite Item {0}: {1}", i, e.Message), e);
                    }
                }
            };
        }

        private static Action<Statement> CheckForStmt(Action<Expression> left, Action<Expression> list, Action<Statement> body, Action<Statement> _else = null) {
            return stmt => {
                Assert.AreEqual(typeof(ForStatement), stmt.GetType());
                var forStmt = (ForStatement)stmt;

                left(forStmt.Left);
                list(forStmt.List);
                body(forStmt.Body);
                if (_else != null) {
                    _else(forStmt.Else);
                } else {
                    Assert.AreEqual(forStmt.Else, null);
                }
            };
        }

        private Action<Statement> CheckAsyncForStmt(Action<Statement> checkForStmt) {
            return stmt => {
                Assert.IsInstanceOfType(stmt, typeof(ForStatement));
                var forStmt = (ForStatement)stmt;

                Assert.IsTrue(forStmt.IsAsync);

                checkForStmt(stmt);
            };
        }

        private static Action<Statement> CheckWhileStmt(Action<Expression> test, Action<Statement> body, Action<Statement> _else = null) {
            return stmt => {
                Assert.AreEqual(typeof(WhileStatement), stmt.GetType());
                var whileStmt = (WhileStatement)stmt;

                test(whileStmt.Test);
                body(whileStmt.Body);
                if (_else != null) {
                    _else(whileStmt.ElseStatement);
                } else {
                    Assert.AreEqual(whileStmt.ElseStatement, null);
                }
            };
        }

        private static Action<TryStatementHandler> CheckHandler(Action<Expression> test, Action<Expression> target, Action<Statement> body) {
            return handler => {
                body(handler.Body);

                if (test != null) {
                    test(handler.Test);
                } else {
                    Assert.AreEqual(null, handler.Test);
                }

                if (target != null) {
                    target(handler.Target);
                } else {
                    Assert.AreEqual(null, handler.Target);
                }
            };
        }

        private static Action<Statement> CheckTryStmt(Action<Statement> body, Action<TryStatementHandler>[] handlers, Action<Statement> _else = null, Action<Statement> _finally = null) {
            return stmt => {
                Assert.AreEqual(typeof(TryStatement), stmt.GetType());
                var tryStmt = (TryStatement)stmt;

                body(tryStmt.Body);

                Assert.AreEqual(handlers.Length, tryStmt.Handlers.Count);
                for (var i = 0; i < handlers.Length; i++) {
                    handlers[i](tryStmt.Handlers[i]);
                }

                if (_else != null) {
                    _else(tryStmt.Else);
                } else {
                    Assert.AreEqual(tryStmt.Else, null);
                }

                if (_finally != null) {
                    _finally(tryStmt.Finally);
                } else {
                    Assert.AreEqual(tryStmt.Finally, null);
                }
            };
        }

        private static Action<Statement> CheckRaiseStmt(Action<Expression> exceptionType = null, Action<Expression> exceptionValue = null, Action<Expression> traceBack = null, Action<Expression> cause = null) {
            return stmt => {
                Assert.AreEqual(typeof(RaiseStatement), stmt.GetType());
                var raiseStmt = (RaiseStatement)stmt;

                if (exceptionType != null) {
                    exceptionType(raiseStmt.ExceptType);
                } else {
                    Assert.AreEqual(raiseStmt.ExceptType, null);
                }

                if (exceptionValue != null) {
                    exceptionValue(raiseStmt.Value);
                } else {
                    Assert.AreEqual(raiseStmt.Value, null);
                }

                if (traceBack != null) {
                    traceBack(raiseStmt.Traceback);
                } else {
                    Assert.AreEqual(raiseStmt.Traceback, null);
                }

            };
        }

        private static Action<Statement> CheckPrintStmt(Action<Expression>[] expressions, Action<Expression> destination = null, bool trailingComma = false) {
            return stmt => {
                Assert.AreEqual(typeof(PrintStatement), stmt.GetType());
                var printStmt = (PrintStatement)stmt;

                Assert.AreEqual(expressions.Length, printStmt.Expressions.Count);
                Assert.AreEqual(printStmt.TrailingComma, trailingComma);

                for (var i = 0; i < expressions.Length; i++) {
                    expressions[i](printStmt.Expressions[i]);
                }

                if (destination != null) {
                    destination(printStmt.Destination);
                } else {
                    Assert.AreEqual(printStmt.Destination, null);
                }
            };
        }


        private static Action<Statement> CheckAssertStmt(Action<Expression> test, Action<Expression> message = null) {
            return stmt => {
                Assert.AreEqual(typeof(AssertStatement), stmt.GetType());
                var assertStmt = (AssertStatement)stmt;

                test(assertStmt.Test);


                if (message != null) {
                    message(assertStmt.Message);
                } else {
                    Assert.AreEqual(assertStmt.Message, null);
                }
            };
        }

        private static Action<IfStatementTest> IfTest(Action<Expression> expectedTest, Action<Statement> body) {
            return test => {
                expectedTest(test.Test);
                body(test.Body);
            };
        }

        private static Action<ImmutableArray<IfStatementTest>> IfTests(params Action<IfStatementTest>[] expectedTests) {
            return tests => {
                Assert.AreEqual(expectedTests.Length, tests.Count);
                for (var i = 0; i < expectedTests.Length; i++) {
                    expectedTests[i](tests[i]);
                }
            };
        }

        private static Action<Statement> CheckIfStmt(Action<ImmutableArray<IfStatementTest>> tests, Action<Statement> _else = null) {
            return stmt => {
                Assert.AreEqual(typeof(IfStatement), stmt.GetType());
                var ifStmt = (IfStatement)stmt;

                tests(ifStmt.Tests);

                if (_else != null) {
                    _else(ifStmt.ElseStatement);
                } else {
                    Assert.AreEqual(null, ifStmt.ElseStatement);
                }
            };
        }

        private static Action<Expression> CheckConditionalExpression(Action<Expression> trueExpression, Action<Expression> test, Action<Expression> falseExpression) {
            return expr => {
                Assert.AreEqual(typeof(ConditionalExpression), expr.GetType(), "Not a Conditional Expression");
                var condExpr = (ConditionalExpression)expr;

                test(condExpr.Test);
                trueExpression(condExpr.TrueExpression);
                falseExpression(condExpr.FalseExpression);
            };
        }

        private static Action<Statement> CheckFromImport(string fromName, string[] names, string[] asNames = null) {
            return stmt => {
                Assert.AreEqual(typeof(FromImportStatement), stmt.GetType());
                var fiStmt = (FromImportStatement)stmt;

                Assert.AreEqual(fiStmt.Root.MakeString(), fromName);
                Assert.AreEqual(names.Length, fiStmt.Names.Count);
                for (var i = 0; i < names.Length; i++) {
                    Assert.AreEqual(names[i], fiStmt.Names[i].Name);
                }

                if (asNames == null) {
                    if (fiStmt.AsNames != null) {
                        for (var i = 0; i < fiStmt.AsNames.Count; i++) {
                            Assert.AreEqual(null, fiStmt.AsNames[i]);
                        }
                    }
                } else {
                    Assert.AreEqual(asNames.Length, fiStmt.AsNames.Count);
                    for (var i = 0; i < asNames.Length; i++) {
                        Assert.AreEqual(asNames[i], fiStmt.AsNames[i].Name);
                    }
                }
            };
        }

        private static Action<Statement> CheckImport(string[] names, string[] asNames = null) {
            return stmt => {
                Assert.AreEqual(typeof(ImportStatement), stmt.GetType());
                var fiStmt = (ImportStatement)stmt;

                Assert.AreEqual(names.Length, fiStmt.Names.Count);
                for (var i = 0; i < names.Length; i++) {
                    Assert.AreEqual(names[i], fiStmt.Names[i].MakeString());
                }

                if (asNames == null) {
                    if (fiStmt.AsNames != null) {
                        for (var i = 0; i < fiStmt.AsNames.Count; i++) {
                            Assert.AreEqual(null, fiStmt.AsNames[i]);
                        }
                    }
                } else {
                    Assert.AreEqual(asNames.Length, fiStmt.AsNames.Count);
                    for (var i = 0; i < asNames.Length; i++) {
                        Assert.AreEqual(asNames[i], fiStmt.AsNames[i].Name);
                    }
                }
            };
        }

        private static Action<Statement> CheckExprStmt(Action<Expression> expr) {
            return stmt => {
                Assert.AreEqual(typeof(ExpressionStatement), stmt.GetType());
                var exprStmt = (ExpressionStatement)stmt;
                expr(exprStmt.Expression);
            };
        }

        private static Action<Statement> CheckConstantStmt(object value) {
            return CheckExprStmt(CheckConstant(value));
        }

        private static Action<Statement> CheckConstantStmtAndRepr(object value, string repr, PythonLanguageVersion ver) {
            return CheckExprStmt(CheckConstant(value, repr, ver));
        }

        private static Action<Statement> CheckLambdaStmt(Action<Parameter>[] args, Action<Expression> body) {
            return CheckExprStmt(CheckLambda(args, body));
        }

        private static Action<Expression> CheckLambda(Action<Parameter>[] args, Action<Expression> body) {
            return expr => {
                Assert.AreEqual(typeof(LambdaExpression), expr.GetType());

                var lambda = (LambdaExpression)expr;

                CheckFuncDef(null, args, (bodyCheck) => CheckReturnStmt(body)(bodyCheck))(lambda.Function);
            };
        }

        private static Action<Statement> CheckReturnStmt(Action<Expression> retVal = null) {
            return stmt => {
                Assert.AreEqual(typeof(ReturnStatement), stmt.GetType());
                var retStmt = (ReturnStatement)stmt;

                if (retVal != null) {
                    retVal(retStmt.Expression);
                } else {
                    Assert.AreEqual(null, retStmt.Expression);
                }
            };
        }

        private static Action<Statement> CheckFuncDef(string name, Action<Parameter>[] args, Action<Statement> body, Action<Expression>[] decorators = null, Action<Expression> returnAnnotation = null) {
            return stmt => {
                Assert.AreEqual(typeof(FunctionDefinition), stmt.GetType());
                var funcDef = (FunctionDefinition)stmt;

                if (name != null) {
                    Assert.AreEqual(name, funcDef.Name);
                }

                Assert.AreEqual(args?.Length ?? 0, funcDef.Parameters.Length);
                for (var i = 0; i < (args?.Length ?? 0); i++) {
                    args[i](funcDef.Parameters[i]);
                }

                body(funcDef.Body);

                if (returnAnnotation != null) {
                    returnAnnotation(funcDef.ReturnAnnotation);
                } else {
                    Assert.AreEqual(null, funcDef.ReturnAnnotation);
                }

                CheckDecorators(decorators, funcDef.Decorators);
            };
        }

        private static Action<Statement> CheckCoroutineDef(Action<Statement> checkFuncDef) {
            return stmt => {
                Assert.IsInstanceOfType(stmt, typeof(FunctionDefinition));
                var funcDef = (FunctionDefinition)stmt;

                Assert.IsTrue(funcDef.IsCoroutine);
                checkFuncDef(stmt);
            };
        }

        private static void CheckDecorators(Action<Expression>[] decorators, DecoratorStatement foundDecorators) {
            if (decorators != null) {
                Assert.AreEqual(decorators.Length, foundDecorators.Decorators.Length);
                for (var i = 0; i < decorators.Length; i++) {
                    decorators[i](foundDecorators.Decorators[i]);
                }
            } else {
                Assert.AreEqual(null, foundDecorators);
            }
        }

        private static Action<Statement> CheckClassDef(string name, Action<Statement> body, Action<Arg>[] bases = null, Action<Expression>[] decorators = null) {
            return stmt => {
                Assert.AreEqual(typeof(ClassDefinition), stmt.GetType());
                var classDef = (ClassDefinition)stmt;

                if (name != null) {
                    Assert.AreEqual(name, classDef.Name);
                }

                if (bases != null) {
                    Assert.AreEqual(bases.Length, classDef.Bases.Count);
                    for (var i = 0; i < bases.Length; i++) {
                        bases[i](classDef.Bases[i]);
                    }
                } else {
                    Assert.AreEqual(0, classDef.Bases.Count);
                }

                body(classDef.Body);

                CheckDecorators(decorators, classDef.Decorators);
            };
        }

        private static Action<Parameter> CheckParameter(string name, ParameterKind kind = ParameterKind.Normal, Action<Expression> defaultValue = null, Action<Expression> annotation = null) {
            return param => {
                Assert.AreEqual(name ?? "", param.Name ?? "");
                Assert.AreEqual(kind, param.Kind);

                if (defaultValue != null) {
                    defaultValue(param.DefaultValue);
                } else {
                    Assert.AreEqual(null, param.DefaultValue);
                }

                if (annotation != null) {
                    annotation(param.Annotation);
                } else {
                    Assert.AreEqual(null, param.Annotation);
                }
            };
        }

        private static Action<Parameter> CheckSublistParameter(params string[] names) {
            return param => {
                Assert.AreEqual(typeof(SublistParameter), param.GetType());
                var sublistParam = (SublistParameter)param;

                Assert.AreEqual(names.Length, sublistParam.Tuple.Items.Count);
                for (var i = 0; i < names.Length; i++) {
                    Assert.AreEqual(names[i], ((NameExpression)sublistParam.Tuple.Items[i]).Name);
                }
            };
        }

        private static Action<Statement> CheckBinaryStmt(Action<Expression> lhs, PythonOperator op, Action<Expression> rhs) {
            return CheckExprStmt(CheckBinaryExpression(lhs, op, rhs));
        }

        private static Action<Expression> CheckBinaryExpression(Action<Expression> lhs, PythonOperator op, Action<Expression> rhs) {
            return expr => {
                Assert.AreEqual(typeof(BinaryExpression), expr.GetType());
                var bin = (BinaryExpression)expr;
                Assert.AreEqual(bin.Operator, op);
                lhs(bin.Left);
                rhs(bin.Right);
            };
        }

        private static Action<Statement> CheckUnaryStmt(PythonOperator op, Action<Expression> value) {
            return CheckExprStmt(CheckUnaryExpression(op, value));
        }

        private static Action<Expression> CheckUnaryExpression(PythonOperator op, Action<Expression> value) {
            return expr => {
                Assert.AreEqual(typeof(UnaryExpression), expr.GetType());
                var unary = (UnaryExpression)expr;
                Assert.AreEqual(unary.Op, op);
                value(unary.Expression);
            };
        }

        private static Action<Statement> CheckBackquoteStmt(Action<Expression> value) {
            return CheckExprStmt(CheckBackquoteExpr(value));
        }

        private static Action<Expression> CheckBackquoteExpr(Action<Expression> value) {
            return expr => {
                Assert.AreEqual(typeof(BackQuoteExpression), expr.GetType());
                var bq = (BackQuoteExpression)expr;
                value(bq.Expression);
            };
        }

        private static Action<Expression> CheckAwaitExpression(Action<Expression> value) {
            return expr => {
                Assert.IsInstanceOfType(expr, typeof(AwaitExpression));
                var await = (AwaitExpression)expr;
                value(await.Expression);
            };
        }
        private static Action<Expression> CheckAndExpression(Action<Expression> lhs, Action<Expression> rhs) {
            return expr => {
                Assert.AreEqual(typeof(AndExpression), expr.GetType());
                var bin = (AndExpression)expr;
                lhs(bin.Left);
                rhs(bin.Right);
            };
        }

        private static Action<Expression> CheckOrExpression(Action<Expression> lhs, Action<Expression> rhs) {
            return expr => {
                Assert.AreEqual(typeof(OrExpression), expr.GetType());
                var bin = (OrExpression)expr;
                lhs(bin.Left);
                rhs(bin.Right);
            };
        }

        private static Action<Statement> CheckCallStmt(Action<Expression> target, params Action<Arg>[] args) {
            return CheckExprStmt(CheckCallExpression(target, args));
        }

        private static Action<Expression> CheckCallExpression(Action<Expression> target, params Action<Arg>[] args) {
            return expr => {
                Assert.AreEqual(typeof(CallExpression), expr.GetType());
                var call = (CallExpression)expr;
                target(call.Target);

                Assert.AreEqual(args.Length, call.Args.Count);
                for (var i = 0; i < args.Length; i++) {
                    args[i](call.Args[i]);
                }
            };
        }

        private static Action<Expression> DictItem(Action<Expression> key, Action<Expression> value) {
            return CheckSlice(key, value);
        }

        private static Action<Expression> CheckSlice(Action<Expression> start, Action<Expression> stop, Action<Expression> step = null) {
            return expr => {
                Assert.AreEqual(typeof(SliceExpression), expr.GetType());
                var slice = (SliceExpression)expr;

                if (start != null) {
                    start(slice.SliceStart);
                } else {
                    Assert.AreEqual(null, slice.SliceStart);
                }

                if (stop != null) {
                    stop(slice.SliceStop);
                } else {
                    Assert.AreEqual(null, slice.SliceStop);
                }

                if (step != null) {
                    step(slice.SliceStep);
                } else {
                    Assert.AreEqual(null, slice.SliceStep);
                }
            };
        }

        private static Action<Statement> CheckMemberStmt(Action<Expression> target, string name) {
            return CheckExprStmt(CheckMemberExpr(target, name));
        }

        private static Action<Expression> CheckMemberExpr(Action<Expression> target, string name) {
            return expr => {
                Assert.IsInstanceOfType(expr, typeof(MemberExpression));
                var member = (MemberExpression)expr;
                Assert.AreEqual(name, member.Name);
                target(member.Target);
            };
        }

        private static Action<Arg> CheckArg(string name) {
            return expr => {
                Assert.AreEqual(null, expr.Name);
                Assert.AreEqual(typeof(NameExpression), expr.Expression.GetType());
                var nameExpr = (NameExpression)expr.Expression;
                Assert.AreEqual(nameExpr.Name, name);
            };
        }


        private static Action<Arg> CheckNamedArg(string argName, Action<Expression> value) {
            return expr => {
                Assert.AreEqual(argName, expr.Name);
                value(expr.Expression);
            };
        }


        private static Action<Expression> CheckNameExpr(string name) {
            return expr => {
                Assert.IsInstanceOfType(expr, typeof(NameExpression));
                var nameExpr = (NameExpression)expr;
                Assert.AreEqual(nameExpr.Name, name);
            };
        }

        private static Action<Statement> CheckNameStmt(string name) {
            return CheckExprStmt(CheckNameExpr(name));
        }

        private static Action<Arg> PositionalArg(Action<Expression> value) {
            return arg => {
                Assert.AreEqual(true, string.IsNullOrEmpty(arg.Name));
                value(arg.Expression);
            };
        }

        private static Action<Arg> NamedArg(string name, Action<Expression> value) {
            return arg => {
                Assert.AreEqual(name, arg.Name);
                value(arg.Expression);
            };
        }

        private static Action<Arg> ListArg(Action<Expression> value) {
            return NamedArg("*", value);
        }

        private static Action<Arg> DictArg(Action<Expression> value) {
            return NamedArg("**", value);
        }

        private static Action<Statement> CheckIndexStmt(Action<Expression> target, Action<Expression> index) {
            return CheckExprStmt(CheckIndexExpression(target, index));
        }

        private static Action<Expression> CheckIndexExpression(Action<Expression> target, Action<Expression> index) {
            return expr => {
                Assert.AreEqual(typeof(IndexExpression), expr.GetType());
                var indexExpr = (IndexExpression)expr;
                target(indexExpr.Target);
                index(indexExpr.Index);
            };
        }

        private static Action<Statement> CheckDictionaryStmt(params Action<SliceExpression>[] items) {
            return CheckExprStmt(CheckDictionaryExpr(items));
        }

        private static Action<Expression> CheckDictionaryExpr(params Action<SliceExpression>[] items) {
            return expr => {
                Assert.AreEqual(typeof(DictionaryExpression), expr.GetType());
                var dictExpr = (DictionaryExpression)expr;
                Assert.AreEqual(items.Length, dictExpr.Items.Count);

                for (var i = 0; i < dictExpr.Items.Count; i++) {
                    items[i](dictExpr.Items[i]);
                }
            };
        }

        private static Action<SliceExpression> CheckDictKeyOnly(Action<Expression> key) {
            return expr => {
                Assert.AreEqual(typeof(DictKeyOnlyExpression), expr.GetType());
                key(((DictKeyOnlyExpression)expr).Key);
            };
        }

        private static Action<SliceExpression> CheckDictValueOnly(Action<Expression> value) {
            return expr => {
                Assert.AreEqual(typeof(DictValueOnlyExpression), expr.GetType());
                value(((DictValueOnlyExpression)expr).Value);
            };
        }

        private static Action<Statement> CheckTupleStmt(params Action<Expression>[] items) {
            return CheckExprStmt(CheckTupleExpr(items));
        }

        private static Action<Expression> CheckTupleExpr(params Action<Expression>[] items) {
            return expr => {
                Assert.AreEqual(typeof(TupleExpression), expr.GetType());
                var tupleExpr = (TupleExpression)expr;
                Assert.AreEqual(items.Length, tupleExpr.Items.Count);

                for (var i = 0; i < tupleExpr.Items.Count; i++) {
                    items[i](tupleExpr.Items[i]);
                }
            };
        }

        private static Action<Expression> CheckListExpr(params Action<Expression>[] items) {
            return expr => {
                Assert.AreEqual(typeof(ListExpression), expr.GetType());
                var listExpr = (ListExpression)expr;
                Assert.AreEqual(items.Length, listExpr.Items.Count);

                for (var i = 0; i < listExpr.Items.Count; i++) {
                    items[i](listExpr.Items[i]);
                }
            };
        }

        private static Action<Statement> CheckAssignment(Action<Expression> lhs, Action<Expression> rhs) {
            return CheckAssignment(new[] { lhs }, rhs);
        }

        private static Action<Statement> CheckAssignment(Action<Expression>[] lhs, Action<Expression> rhs) {
            return expr => {
                Assert.AreEqual(typeof(AssignmentStatement), expr.GetType());
                var assign = (AssignmentStatement)expr;

                Assert.AreEqual(assign.Left.Count, lhs.Length);
                for (var i = 0; i < lhs.Length; i++) {
                    lhs[i](assign.Left[i]);
                }
                rhs(assign.Right);
            };
        }

        private static Action<Expression> CheckNamedExpr(Action<Expression> target, Action<Expression> value) {
            return expr => {
                Assert.AreEqual(typeof(NamedExpression), expr.GetType());
                var assignExpr = (NamedExpression)expr;

                target(assignExpr.Target);
                value(assignExpr.Value);
            };
        }

        private static Action<Expression> CheckErrorExpr() {
            return expr => {
                Assert.AreEqual(typeof(ErrorExpression), expr.GetType());
            };
        }

        private static Action<Statement> CheckErrorStmt() {
            return expr => {
                Assert.AreEqual(typeof(ErrorStatement), expr.GetType());
            };
        }

        private static Action<Statement> CheckEmptyStmt() {
            return expr => {
                Assert.AreEqual(typeof(EmptyStatement), expr.GetType());
            };
        }

        private static Action<Statement> CheckBreakStmt() {
            return expr => {
                Assert.AreEqual(typeof(BreakStatement), expr.GetType());
            };
        }

        private static Action<Statement> CheckContinueStmt() {
            return expr => {
                Assert.AreEqual(typeof(ContinueStatement), expr.GetType());
            };
        }

        private static Action<Statement> CheckAssignment(Action<Expression> lhs, PythonOperator op, Action<Expression> rhs) {
            return stmt => {
                Assert.AreEqual(typeof(AugmentedAssignStatement), stmt.GetType());
                var assign = (AugmentedAssignStatement)stmt;

                Assert.AreEqual(assign.Operator, op);

                lhs(assign.Left);
                rhs(assign.Right);
            };
        }

        private Action<Statement> CheckExecStmt(Action<Expression> code, Action<Expression> globals = null, Action<Expression> locals = null) {
            return stmt => {
                Assert.AreEqual(typeof(ExecStatement), stmt.GetType());
                var exec = (ExecStatement)stmt;

                code(exec.Code);
                if (globals != null) {
                    globals(exec.Globals);
                } else {
                    Assert.AreEqual(null, exec.Globals);
                }

                if (locals != null) {
                    locals(exec.Locals);
                } else {
                    Assert.AreEqual(null, exec.Locals);
                }
            };
        }

        private Action<Statement> CheckWithStmt(Action<Expression> expr, Action<Statement> body) {
            return CheckWithStmt(expr, null, body);
        }

        private Action<Statement> CheckWithStmt(Action<Expression> expr, Action<Expression> target, Action<Statement> body) {
            return CheckWithStmt(new[] { expr }, new[] { target }, body);
        }

        private Action<Statement> CheckWithStmt(Action<Expression>[] expr, Action<Statement> body) {
            return CheckWithStmt(expr, new Action<Expression>[expr.Length], body);
        }

        private Action<Statement> CheckWithStmt(Action<Expression>[] expr, Action<Expression>[] target, Action<Statement> body) {
            return stmt => {
                Assert.AreEqual(typeof(WithStatement), stmt.GetType());
                var with = (WithStatement)stmt;

                Assert.AreEqual(expr.Length, with.Items.Count);
                for (var i = 0; i < with.Items.Count; i++) {
                    expr[i](with.Items[i].ContextManager);

                    if (target[i] != null) {
                        target[i](with.Items[i].Variable);
                    } else {
                        Assert.AreEqual(null, with.Items[0].Variable);
                    }
                }

                body(with.Body);
            };
        }

        private Action<Statement> CheckAsyncWithStmt(Action<Statement> checkWithStmt) {
            return stmt => {
                Assert.IsInstanceOfType(stmt, typeof(WithStatement));
                var withStmt = (WithStatement)stmt;

                Assert.IsTrue(withStmt.IsAsync);

                checkWithStmt(stmt);
            };
        }

        private static Action<Node> CheckNodeConstant(object value, string expectedRepr = null, PythonLanguageVersion ver = PythonLanguageVersion.V27) {
            return node => {
                Assert.IsInstanceOfType(node, typeof(Expression));
                CheckConstant(value)((Expression)node);
            };
        }

        private static Action<Expression> CheckConstant(object value, string expectedRepr = null, PythonLanguageVersion ver = PythonLanguageVersion.V27) {
            return expr => {
                Assert.AreEqual(typeof(ConstantExpression), expr.GetType());

                if (value is byte[]) {
                    Assert.AreEqual(typeof(AsciiString), ((ConstantExpression)expr).Value.GetType());
                    var b1 = (byte[])value;
                    var b2 = ((AsciiString)((ConstantExpression)expr).Value).Bytes.ToArray();
                    Assert.AreEqual(b1.Length, b2.Length);

                    for (var i = 0; i < b1.Length; i++) {
                        Assert.AreEqual(b1[i], b2[i]);
                    }
                } else {
                    Assert.AreEqual(value, ((ConstantExpression)expr).Value);
                }

                if (expectedRepr != null) {
                    Assert.AreEqual(expectedRepr, ((ConstantExpression)expr).GetConstantRepr(ver));
                }
            };
        }

        private Action<Statement> CheckDelStmt(params Action<Expression>[] deletes) {
            return stmt => {
                Assert.AreEqual(typeof(DelStatement), stmt.GetType());
                var del = (DelStatement)stmt;

                Assert.AreEqual(deletes.Length, del.Expressions.Count);
                for (var i = 0; i < deletes.Length; i++) {
                    deletes[i](del.Expressions[i]);
                }
            };
        }

        private Action<Expression> CheckParenExpr(Action<Expression> value) {
            return expr => {
                Assert.AreEqual(typeof(ParenthesisExpression), expr.GetType());
                var paren = (ParenthesisExpression)expr;

                value(paren.Expression);
            };
        }

        private Action<Expression> CheckStarExpr(Action<Expression> value, int starCount = 1) {
            return expr => {
                Assert.AreEqual(typeof(StarredExpression), expr.GetType());
                var starred = (StarredExpression)expr;
                Assert.AreEqual(starCount, starred.StarCount);

                value(starred.Expression);
            };
        }

        private Action<Statement> CheckGlobal(params string[] names) {
            return stmt => {
                Assert.AreEqual(typeof(GlobalStatement), stmt.GetType());
                var global = (GlobalStatement)stmt;

                Assert.AreEqual(names.Length, global.Names.Count);
                for (var i = 0; i < names.Length; i++) {
                    Assert.AreEqual(names[i], global.Names[i].Name);
                }
            };
        }

        private Action<Statement> CheckNonlocal(params string[] names) {
            return stmt => {
                Assert.AreEqual(typeof(NonlocalStatement), stmt.GetType());
                var nonlocal = (NonlocalStatement)stmt;

                Assert.AreEqual(names.Length, nonlocal.Names.Count);
                for (var i = 0; i < names.Length; i++) {
                    Assert.AreEqual(names[i], nonlocal.Names[i].Name);
                }
            };
        }

        private Action<Statement> CheckStrOrBytesStmt(PythonLanguageVersion version, string str)
            => CheckExprStmt(CheckStrOrBytes(version, str));

        private Action<Expression> CheckStrOrBytes(PythonLanguageVersion version, string str) {
            return expr => {
                if (version.Is2x()) {
                    CheckConstant(ToBytes(str));
                } else {
                    CheckConstant(str);
                }
            };
        }

        private Action<Statement> CheckYieldStmt(Action<Expression> value) {
            return CheckExprStmt(CheckYieldExpr(value));
        }

        private Action<Expression> CheckYieldExpr(Action<Expression> value) {
            return expr => {
                Assert.AreEqual(typeof(YieldExpression), expr.GetType());
                var yield = (YieldExpression)expr;

                value(yield.Expression);
            };
        }

        private Action<Statement> CheckYieldFromStmt(Action<Expression> value) {
            return CheckExprStmt(CheckYieldFromExpr(value));
        }

        private Action<Expression> CheckYieldFromExpr(Action<Expression> value) {
            return expr => {
                Assert.AreEqual(typeof(YieldFromExpression), expr.GetType());
                var yield = (YieldFromExpression)expr;

                value(yield.Expression);
            };
        }

        private Action<Expression> CheckListComp(Action<Expression> item, params Action<ComprehensionIterator>[] iterators) {
            return expr => {
                Assert.AreEqual(typeof(ListComprehension), expr.GetType());
                var listComp = (ListComprehension)expr;

                Assert.AreEqual(iterators.Length, listComp.Iterators.Count);

                item(listComp.Item);
                for (var i = 0; i < iterators.Length; i++) {
                    iterators[i](listComp.Iterators[i]);
                }
            };
        }

        private Action<Expression> CheckGeneratorComp(Action<Expression> item, params Action<ComprehensionIterator>[] iterators) {
            return expr => {
                Assert.AreEqual(typeof(GeneratorExpression), expr.GetType());
                var listComp = (GeneratorExpression)expr;

                Assert.AreEqual(iterators.Length, listComp.Iterators.Count);

                item(listComp.Item);
                for (var i = 0; i < iterators.Length; i++) {
                    iterators[i](listComp.Iterators[i]);
                }
            };
        }

        private Action<Expression> CheckDictComp(Action<Expression> key, Action<Expression> value, params Action<ComprehensionIterator>[] iterators) {
            return expr => {
                Assert.AreEqual(typeof(DictionaryComprehension), expr.GetType());
                var dictComp = (DictionaryComprehension)expr;

                Assert.AreEqual(iterators.Length, dictComp.Iterators.Count);

                key(dictComp.Key);
                value(dictComp.Value);

                for (var i = 0; i < iterators.Length; i++) {
                    iterators[i](dictComp.Iterators[i]);
                }
            };
        }

        private Action<Expression> CheckSetComp(Action<Expression> item, params Action<ComprehensionIterator>[] iterators) {
            return expr => {
                Assert.AreEqual(typeof(SetComprehension), expr.GetType());
                var setComp = (SetComprehension)expr;

                Assert.AreEqual(iterators.Length, setComp.Iterators.Count);

                item(setComp.Item);

                for (var i = 0; i < iterators.Length; i++) {
                    iterators[i](setComp.Iterators[i]);
                }
            };
        }

        private Action<Expression> CheckSetLiteral(params Action<Expression>[] values) {
            return expr => {
                Assert.AreEqual(typeof(SetExpression), expr.GetType());
                var setLiteral = (SetExpression)expr;

                Assert.AreEqual(values.Length, setLiteral.Items.Count);
                for (var i = 0; i < values.Length; i++) {
                    values[i](setLiteral.Items[i]);
                }
            };
        }

        private static Action<Expression> CheckFString(params Action<Node>[] subExpressions) {
            return expr => {
                Assert.AreEqual(typeof(FString), expr.GetType());
                var nodes = expr.GetChildNodes().ToArray();
                Assert.AreEqual(nodes.Length, subExpressions.Length, "Wrong amount of nodes in fstring");
                for (var i = 0; i < subExpressions.Length; i++) {
                    subExpressions[i](nodes[i]);
                }
            };
        }

        private static Action<Node> CheckFormattedValue(Action<Expression> value, char? conversion = null, Action<Expression> formatSpecifier = null) {
            return node => {
                Assert.AreEqual(typeof(FormattedValue), node.GetType());
                var formattedValue = (FormattedValue)node;

                value(formattedValue.Value);
                Assert.AreEqual(formattedValue.Conversion, conversion, "formatted value's conversion is not correct");
                if (formatSpecifier == null) {
                    Assert.AreEqual(formattedValue.FormatSpecifier, null, "format specifier is not null");
                } else {
                    formatSpecifier(formattedValue.FormatSpecifier);
                }
            };
        }

        private static Action<Expression> CheckFormatSpecifer(params Action<Node>[] subExpressions) {
            return expr => {
                Assert.AreEqual(typeof(FormatSpecifier), expr.GetType());

                var nodes = expr.GetChildNodes().ToArray();
                Assert.AreEqual(nodes.Length, subExpressions.Length, "Wrong amount of nodes in format specifier");
                for (var i = 0; i < subExpressions.Length; i++) {
                    subExpressions[i](nodes[i]);
                }
            };
        }

        private Action<ComprehensionIterator> CompFor(Action<Expression> lhs, Action<Expression> list) {
            return iter => {
                Assert.AreEqual(typeof(ComprehensionFor), iter.GetType());
                var forIter = (ComprehensionFor)iter;

                lhs(forIter.Left);
                list(forIter.List);
            };
        }

        private Action<ComprehensionIterator> AsyncCompFor(Action<Expression> lhs, Action<Expression> list) {
            return iter => {
                Assert.AreEqual(typeof(ComprehensionFor), iter.GetType());
                var forIter = (ComprehensionFor)iter;
                Assert.IsTrue(forIter.IsAsync);

                lhs(forIter.Left);
                list(forIter.List);
            };
        }

        private Action<ComprehensionIterator> CompIf(Action<Expression> test) {
            return iter => {
                Assert.AreEqual(typeof(ComprehensionIf), iter.GetType());
                var ifIter = (ComprehensionIf)iter;

                test(ifIter.Test);
            };
        }

        private byte[] ToBytes(string str) {
            var res = new byte[str.Length];
            for (var i = 0; i < str.Length; i++) {
                res[i] = (byte)str[i];
            }
            return res;
        }

        private static Action<Expression> IgnoreExpr() {
            return expr => { };
        }

        private static Action<Statement> IgnoreStmt() {
            return stmt => { };
        }

        private static readonly Action<Parameter>[] NoParameters = new Action<Parameter>[0];

        private static void CollectFiles(string dir, List<string> files, IEnumerable<string> exceptions = null) {
            foreach (var file in Directory.GetFiles(dir)) {
                if (file.EndsWithOrdinal(".py", ignoreCase: true)) {
                    files.Add(file);
                }
            }
            foreach (var nestedDir in Directory.GetDirectories(dir)) {
                if (exceptions == null || !exceptions.Contains(Path.GetFileName(nestedDir))) {
                    CollectFiles(nestedDir, files, exceptions);
                }
            }
        }

        #endregion
    }
}
