﻿// Python Tools for Visual Studio
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
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Python.Analysis.Core.DependencyResolution;
using Microsoft.Python.Analysis.Core.Interpreter;
using Microsoft.Python.Core;
using Microsoft.Python.Core.Collections;
using Microsoft.Python.Parsing.Ast;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Parsing.Tests {
    /// <summary>
    /// Test cases to verify that the parser successfully preserves all information for round tripping source code.
    /// </summary>
    [TestClass]
    public class ParserRoundTripTest {
        [TestMethod, Priority(1)]
        public void TestCodeFormattingOptions() {
            /* Function Definitions */
            // SpaceAroundDefaultValueEquals
            TestOneString(PythonLanguageVersion.V27, "def f(a=2): pass", new CodeFormattingOptions() { SpaceAroundDefaultValueEquals = true }, "def f(a = 2): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(a = 2): pass", new CodeFormattingOptions() { SpaceAroundDefaultValueEquals = false }, "def f(a=2): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(a=2): pass", new CodeFormattingOptions() { SpaceAroundDefaultValueEquals = false }, "def f(a=2): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(a = 2): pass", new CodeFormattingOptions() { SpaceAroundDefaultValueEquals = true }, "def f(a = 2): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(a=2): pass", new CodeFormattingOptions() { SpaceAroundDefaultValueEquals = null }, "def f(a=2): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(a = 2): pass", new CodeFormattingOptions() { SpaceAroundDefaultValueEquals = null }, "def f(a = 2): pass");

            // SpaceBeforeMethodDeclarationParen
            TestOneString(PythonLanguageVersion.V27, "def f(): pass", new CodeFormattingOptions() { SpaceBeforeFunctionDeclarationParen = true }, "def f (): pass");
            TestOneString(PythonLanguageVersion.V27, "def f (): pass", new CodeFormattingOptions() { SpaceBeforeFunctionDeclarationParen = true }, "def f (): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(): pass", new CodeFormattingOptions() { SpaceBeforeFunctionDeclarationParen = false }, "def f(): pass");
            TestOneString(PythonLanguageVersion.V27, "def f (): pass", new CodeFormattingOptions() { SpaceBeforeFunctionDeclarationParen = false }, "def f(): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(): pass", new CodeFormattingOptions() { SpaceBeforeFunctionDeclarationParen = null }, "def f(): pass");
            TestOneString(PythonLanguageVersion.V27, "def f (): pass", new CodeFormattingOptions() { SpaceBeforeFunctionDeclarationParen = null }, "def f (): pass");

            // SpaceWithinEmptyArgumentList
            TestOneString(PythonLanguageVersion.V27, "def f(): pass", new CodeFormattingOptions() { SpaceWithinEmptyParameterList = true }, "def f( ): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(a): pass", new CodeFormattingOptions() { SpaceWithinEmptyParameterList = true }, "def f(a): pass");
            TestOneString(PythonLanguageVersion.V27, "def f( ): pass", new CodeFormattingOptions() { SpaceWithinEmptyParameterList = false }, "def f(): pass");
            TestOneString(PythonLanguageVersion.V27, "def f( a ): pass", new CodeFormattingOptions() { SpaceWithinEmptyParameterList = false }, "def f( a ): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(): pass", new CodeFormattingOptions() { SpaceWithinEmptyParameterList = null }, "def f(): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(a): pass", new CodeFormattingOptions() { SpaceWithinEmptyParameterList = null }, "def f(a): pass");
            TestOneString(PythonLanguageVersion.V27, "def f( ): pass", new CodeFormattingOptions() { SpaceWithinEmptyParameterList = null }, "def f( ): pass");
            TestOneString(PythonLanguageVersion.V27, "def f( a ): pass", new CodeFormattingOptions() { SpaceWithinEmptyParameterList = null }, "def f( a ): pass");

            // SpaceWithinMethodDeclarationParens
            TestOneString(PythonLanguageVersion.V27, "def f(a): pass", new CodeFormattingOptions() { SpaceWithinFunctionDeclarationParens = true }, "def f( a ): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(a, b): pass", new CodeFormattingOptions() { SpaceWithinFunctionDeclarationParens = true }, "def f( a, b ): pass");
            TestOneString(PythonLanguageVersion.V33, "def f(*, a): pass", new CodeFormattingOptions() { SpaceWithinFunctionDeclarationParens = true }, "def f( *, a ): pass");
            TestOneString(PythonLanguageVersion.V27, "def f( a ): pass", new CodeFormattingOptions() { SpaceWithinFunctionDeclarationParens = false }, "def f(a): pass");
            TestOneString(PythonLanguageVersion.V27, "def f( a, b ): pass", new CodeFormattingOptions() { SpaceWithinFunctionDeclarationParens = false }, "def f(a, b): pass");
            TestOneString(PythonLanguageVersion.V33, "def f( *, a ): pass", new CodeFormattingOptions() { SpaceWithinFunctionDeclarationParens = false }, "def f(*, a): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(a): pass", new CodeFormattingOptions() { SpaceWithinFunctionDeclarationParens = null }, "def f(a): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(a, b): pass", new CodeFormattingOptions() { SpaceWithinFunctionDeclarationParens = null }, "def f(a, b): pass");
            TestOneString(PythonLanguageVersion.V33, "def f(*, a): pass", new CodeFormattingOptions() { SpaceWithinFunctionDeclarationParens = null }, "def f(*, a): pass");
            TestOneString(PythonLanguageVersion.V27, "def f( a ): pass", new CodeFormattingOptions() { SpaceWithinFunctionDeclarationParens = null }, "def f( a ): pass");
            TestOneString(PythonLanguageVersion.V27, "def f( a, b ): pass", new CodeFormattingOptions() { SpaceWithinFunctionDeclarationParens = null }, "def f( a, b ): pass");
            TestOneString(PythonLanguageVersion.V33, "def f( *, a ): pass", new CodeFormattingOptions() { SpaceWithinFunctionDeclarationParens = null }, "def f( *, a ): pass");

            // SpaceAroundAnnotationArrow
            TestOneString(PythonLanguageVersion.V33, "def f() -> 42: pass", new CodeFormattingOptions() { SpaceAroundAnnotationArrow = true }, "def f() -> 42: pass");
            TestOneString(PythonLanguageVersion.V33, "def f()->42: pass", new CodeFormattingOptions() { SpaceAroundAnnotationArrow = true }, "def f() -> 42: pass");
            TestOneString(PythonLanguageVersion.V33, "def f()  ->  42: pass", new CodeFormattingOptions() { SpaceAroundAnnotationArrow = true }, "def f() -> 42: pass");
            TestOneString(PythonLanguageVersion.V33, "def f() -> 42: pass", new CodeFormattingOptions() { SpaceAroundAnnotationArrow = false }, "def f()->42: pass");
            TestOneString(PythonLanguageVersion.V33, "def f()->42: pass", new CodeFormattingOptions() { SpaceAroundAnnotationArrow = false }, "def f()->42: pass");
            TestOneString(PythonLanguageVersion.V33, "def f()  ->  42: pass", new CodeFormattingOptions() { SpaceAroundAnnotationArrow = false }, "def f()->42: pass");
            TestOneString(PythonLanguageVersion.V33, "def f() -> 42: pass", new CodeFormattingOptions() { SpaceAroundAnnotationArrow = null }, "def f() -> 42: pass");
            TestOneString(PythonLanguageVersion.V33, "def f()->42: pass", new CodeFormattingOptions() { SpaceAroundAnnotationArrow = null }, "def f()->42: pass");
            TestOneString(PythonLanguageVersion.V33, "def f()  ->  42: pass", new CodeFormattingOptions() { SpaceAroundAnnotationArrow = null }, "def f()  ->  42: pass");

            // SpaceBeforeClassDeclarationParen
            TestOneString(PythonLanguageVersion.V27, "class fob(): pass", new CodeFormattingOptions() { SpaceBeforeClassDeclarationParen = true }, "class fob (): pass");
            TestOneString(PythonLanguageVersion.V27, "class fob (): pass", new CodeFormattingOptions() { SpaceBeforeClassDeclarationParen = true }, "class fob (): pass");
            TestOneString(PythonLanguageVersion.V27, "class fob(): pass", new CodeFormattingOptions() { SpaceBeforeClassDeclarationParen = false }, "class fob(): pass");
            TestOneString(PythonLanguageVersion.V27, "class fob (): pass", new CodeFormattingOptions() { SpaceBeforeClassDeclarationParen = false }, "class fob(): pass");
            TestOneString(PythonLanguageVersion.V27, "class fob(): pass", new CodeFormattingOptions() { SpaceBeforeClassDeclarationParen = null }, "class fob(): pass");
            TestOneString(PythonLanguageVersion.V27, "class fob (): pass", new CodeFormattingOptions() { SpaceBeforeClassDeclarationParen = null }, "class fob (): pass");

            // SpaceWithinEmptyBaseClassListList
            TestOneString(PythonLanguageVersion.V27, "class fob(): pass", new CodeFormattingOptions() { SpaceWithinEmptyBaseClassList = true }, "class fob( ): pass");
            TestOneString(PythonLanguageVersion.V27, "class fob(a): pass", new CodeFormattingOptions() { SpaceWithinEmptyBaseClassList = true }, "class fob(a): pass");
            TestOneString(PythonLanguageVersion.V27, "class fob( ): pass", new CodeFormattingOptions() { SpaceWithinEmptyBaseClassList = false }, "class fob(): pass");
            TestOneString(PythonLanguageVersion.V27, "class fob( a ): pass", new CodeFormattingOptions() { SpaceWithinEmptyBaseClassList = false }, "class fob( a ): pass");
            TestOneString(PythonLanguageVersion.V27, "class fob(): pass", new CodeFormattingOptions() { SpaceWithinEmptyBaseClassList = null }, "class fob(): pass");
            TestOneString(PythonLanguageVersion.V27, "class fob(a): pass", new CodeFormattingOptions() { SpaceWithinEmptyBaseClassList = null }, "class fob(a): pass");
            TestOneString(PythonLanguageVersion.V27, "class fob( ): pass", new CodeFormattingOptions() { SpaceWithinEmptyBaseClassList = null }, "class fob( ): pass");
            TestOneString(PythonLanguageVersion.V27, "class fob( a ): pass", new CodeFormattingOptions() { SpaceWithinEmptyBaseClassList = null }, "class fob( a ): pass");

            // SpaceWithinClassDeclarationParens
            TestOneString(PythonLanguageVersion.V27, "class fob(a): pass", new CodeFormattingOptions() { SpaceWithinClassDeclarationParens = true }, "class fob( a ): pass");
            TestOneString(PythonLanguageVersion.V27, "class fob(a, b): pass", new CodeFormattingOptions() { SpaceWithinClassDeclarationParens = true }, "class fob( a, b ): pass");
            TestOneString(PythonLanguageVersion.V27, "class fob( a ): pass", new CodeFormattingOptions() { SpaceWithinClassDeclarationParens = false }, "class fob(a): pass");
            TestOneString(PythonLanguageVersion.V27, "class fob( a, b ): pass", new CodeFormattingOptions() { SpaceWithinClassDeclarationParens = false }, "class fob(a, b): pass");
            TestOneString(PythonLanguageVersion.V27, "class fob(a): pass", new CodeFormattingOptions() { SpaceWithinClassDeclarationParens = null }, "class fob(a): pass");
            TestOneString(PythonLanguageVersion.V27, "class fob(a, b): pass", new CodeFormattingOptions() { SpaceWithinClassDeclarationParens = null }, "class fob(a, b): pass");
            TestOneString(PythonLanguageVersion.V27, "class fob( a ): pass", new CodeFormattingOptions() { SpaceWithinClassDeclarationParens = null }, "class fob( a ): pass");
            TestOneString(PythonLanguageVersion.V27, "class fob( a, b ): pass", new CodeFormattingOptions() { SpaceWithinClassDeclarationParens = null }, "class fob( a, b ): pass");

            /* Calls */
            // SpaceBeforeCallParen
            TestOneString(PythonLanguageVersion.V27, "f(a)", new CodeFormattingOptions() { SpaceBeforeCallParen = true }, "f (a)");
            TestOneString(PythonLanguageVersion.V27, "f (a)", new CodeFormattingOptions() { SpaceBeforeCallParen = false }, "f(a)");
            TestOneString(PythonLanguageVersion.V27, "f(a)", new CodeFormattingOptions() { SpaceBeforeCallParen = false }, "f(a)");
            TestOneString(PythonLanguageVersion.V27, "f (a)", new CodeFormattingOptions() { SpaceBeforeCallParen = true }, "f (a)");
            TestOneString(PythonLanguageVersion.V27, "f  (a)", new CodeFormattingOptions() { SpaceBeforeCallParen = true }, "f (a)");
            TestOneString(PythonLanguageVersion.V27, "f(a)", new CodeFormattingOptions() { SpaceBeforeCallParen = null }, "f(a)");
            TestOneString(PythonLanguageVersion.V27, "f (a)", new CodeFormattingOptions() { SpaceBeforeCallParen = null }, "f (a)");

            // SpaceWithinEmptyCallArgumentList
            TestOneString(PythonLanguageVersion.V27, "fob()", new CodeFormattingOptions() { SpaceWithinEmptyCallArgumentList = true }, "fob( )");
            TestOneString(PythonLanguageVersion.V27, "fob(a)", new CodeFormattingOptions() { SpaceWithinEmptyCallArgumentList = true }, "fob(a)");
            TestOneString(PythonLanguageVersion.V27, "fob( )", new CodeFormattingOptions() { SpaceWithinEmptyCallArgumentList = false }, "fob()");
            TestOneString(PythonLanguageVersion.V27, "fob( a )", new CodeFormattingOptions() { SpaceWithinEmptyCallArgumentList = false }, "fob( a )");
            TestOneString(PythonLanguageVersion.V27, "fob()", new CodeFormattingOptions() { SpaceWithinEmptyCallArgumentList = null }, "fob()");
            TestOneString(PythonLanguageVersion.V27, "fob(a)", new CodeFormattingOptions() { SpaceWithinEmptyCallArgumentList = null }, "fob(a)");
            TestOneString(PythonLanguageVersion.V27, "fob( )", new CodeFormattingOptions() { SpaceWithinEmptyCallArgumentList = null }, "fob( )");
            TestOneString(PythonLanguageVersion.V27, "fob( a )", new CodeFormattingOptions() { SpaceWithinEmptyCallArgumentList = null }, "fob( a )");

            // SpaceWithinCallParens
            TestOneString(PythonLanguageVersion.V27, "fob(a)", new CodeFormattingOptions() { SpaceWithinCallParens = true }, "fob( a )");
            TestOneString(PythonLanguageVersion.V27, "fob(a, b)", new CodeFormattingOptions() { SpaceWithinCallParens = true }, "fob( a, b )");
            TestOneString(PythonLanguageVersion.V27, "fob( a )", new CodeFormattingOptions() { SpaceWithinCallParens = false }, "fob(a)");
            TestOneString(PythonLanguageVersion.V27, "fob( a, b )", new CodeFormattingOptions() { SpaceWithinCallParens = false }, "fob(a, b)");
            TestOneString(PythonLanguageVersion.V27, "fob(a)", new CodeFormattingOptions() { SpaceWithinCallParens = null }, "fob(a)");
            TestOneString(PythonLanguageVersion.V27, "fob(a, b)", new CodeFormattingOptions() { SpaceWithinCallParens = null }, "fob(a, b)");
            TestOneString(PythonLanguageVersion.V27, "fob( a )", new CodeFormattingOptions() { SpaceWithinCallParens = null }, "fob( a )");
            TestOneString(PythonLanguageVersion.V27, "fob( a, b )", new CodeFormattingOptions() { SpaceWithinCallParens = null }, "fob( a, b )");

            /* Index Expressions */
            // SpaceWithinIndexBrackets
            TestOneString(PythonLanguageVersion.V27, "fob[a]", new CodeFormattingOptions() { SpaceWithinIndexBrackets = true }, "fob[ a ]");
            TestOneString(PythonLanguageVersion.V27, "fob[a, b]", new CodeFormattingOptions() { SpaceWithinIndexBrackets = true }, "fob[ a, b ]");
            TestOneString(PythonLanguageVersion.V27, "fob[ a ]", new CodeFormattingOptions() { SpaceWithinIndexBrackets = false }, "fob[a]");
            TestOneString(PythonLanguageVersion.V27, "fob[ a, b ]", new CodeFormattingOptions() { SpaceWithinIndexBrackets = false }, "fob[a, b]");
            TestOneString(PythonLanguageVersion.V27, "fob[a]", new CodeFormattingOptions() { SpaceWithinIndexBrackets = null }, "fob[a]");
            TestOneString(PythonLanguageVersion.V27, "fob[a, b]", new CodeFormattingOptions() { SpaceWithinIndexBrackets = null }, "fob[a, b]");
            TestOneString(PythonLanguageVersion.V27, "fob[ a ]", new CodeFormattingOptions() { SpaceWithinIndexBrackets = null }, "fob[ a ]");
            TestOneString(PythonLanguageVersion.V27, "fob[ a, b ]", new CodeFormattingOptions() { SpaceWithinIndexBrackets = null }, "fob[ a, b ]");

            // SpaceBeforeIndexBracket
            TestOneString(PythonLanguageVersion.V27, "f[a]", new CodeFormattingOptions() { SpaceBeforeIndexBracket = true }, "f [a]");
            TestOneString(PythonLanguageVersion.V27, "f [a]", new CodeFormattingOptions() { SpaceBeforeIndexBracket = false }, "f[a]");
            TestOneString(PythonLanguageVersion.V27, "f[a]", new CodeFormattingOptions() { SpaceBeforeIndexBracket = false }, "f[a]");
            TestOneString(PythonLanguageVersion.V27, "f [a]", new CodeFormattingOptions() { SpaceBeforeIndexBracket = true }, "f [a]");
            TestOneString(PythonLanguageVersion.V27, "f  [a]", new CodeFormattingOptions() { SpaceBeforeIndexBracket = true }, "f [a]");
            TestOneString(PythonLanguageVersion.V27, "f[a]", new CodeFormattingOptions() { SpaceBeforeIndexBracket = null }, "f[a]");
            TestOneString(PythonLanguageVersion.V27, "f [a]", new CodeFormattingOptions() { SpaceBeforeIndexBracket = null }, "f [a]");

            /* Other */
            // SpacesWithinParenthesisExpression
            TestOneString(PythonLanguageVersion.V27, "(a)", new CodeFormattingOptions() { SpacesWithinParenthesisExpression = true }, "( a )");
            TestOneString(PythonLanguageVersion.V27, "( a )", new CodeFormattingOptions() { SpacesWithinParenthesisExpression = false }, "(a)");
            TestOneString(PythonLanguageVersion.V27, "(a)", new CodeFormattingOptions() { SpacesWithinParenthesisExpression = null }, "(a)");
            TestOneString(PythonLanguageVersion.V27, "( a )", new CodeFormattingOptions() { SpacesWithinParenthesisExpression = null }, "( a )");

            // WithinEmptyTupleExpression
            TestOneString(PythonLanguageVersion.V27, "()", new CodeFormattingOptions() { SpaceWithinEmptyTupleExpression = true }, "( )");
            TestOneString(PythonLanguageVersion.V27, "( )", new CodeFormattingOptions() { SpaceWithinEmptyTupleExpression = true }, "( )");
            TestOneString(PythonLanguageVersion.V27, "()", new CodeFormattingOptions() { SpaceWithinEmptyTupleExpression = false }, "()");
            TestOneString(PythonLanguageVersion.V27, "( )", new CodeFormattingOptions() { SpaceWithinEmptyTupleExpression = false }, "()");
            TestOneString(PythonLanguageVersion.V27, "()", new CodeFormattingOptions() { SpaceWithinEmptyTupleExpression = null }, "()");
            TestOneString(PythonLanguageVersion.V27, "( )", new CodeFormattingOptions() { SpaceWithinEmptyTupleExpression = null }, "( )");

            // WithinParenthesisedTupleExpression
            TestOneString(PythonLanguageVersion.V27, "(a,)", new CodeFormattingOptions() { SpacesWithinParenthesisedTupleExpression = true }, "( a, )");
            TestOneString(PythonLanguageVersion.V27, "(a,b)", new CodeFormattingOptions() { SpacesWithinParenthesisedTupleExpression = true }, "( a,b )");
            TestOneString(PythonLanguageVersion.V27, "( a, )", new CodeFormattingOptions() { SpacesWithinParenthesisedTupleExpression = true }, "( a, )");
            TestOneString(PythonLanguageVersion.V27, "( a,b )", new CodeFormattingOptions() { SpacesWithinParenthesisedTupleExpression = true }, "( a,b )");
            TestOneString(PythonLanguageVersion.V27, "(a,)", new CodeFormattingOptions() { SpacesWithinParenthesisedTupleExpression = false }, "(a,)");
            TestOneString(PythonLanguageVersion.V27, "(a,b)", new CodeFormattingOptions() { SpacesWithinParenthesisedTupleExpression = false }, "(a,b)");
            TestOneString(PythonLanguageVersion.V27, "( a, )", new CodeFormattingOptions() { SpacesWithinParenthesisedTupleExpression = false }, "(a,)");
            TestOneString(PythonLanguageVersion.V27, "( a,b )", new CodeFormattingOptions() { SpacesWithinParenthesisedTupleExpression = false }, "(a,b)");
            TestOneString(PythonLanguageVersion.V27, "(a,)", new CodeFormattingOptions() { SpacesWithinParenthesisedTupleExpression = null }, "(a,)");
            TestOneString(PythonLanguageVersion.V27, "(a,b)", new CodeFormattingOptions() { SpacesWithinParenthesisedTupleExpression = null }, "(a,b)");
            TestOneString(PythonLanguageVersion.V27, "( a, )", new CodeFormattingOptions() { SpacesWithinParenthesisedTupleExpression = null }, "( a, )");
            TestOneString(PythonLanguageVersion.V27, "( a,b )", new CodeFormattingOptions() { SpacesWithinParenthesisedTupleExpression = null }, "( a,b )");

            // WithinEmptyListExpression
            TestOneString(PythonLanguageVersion.V27, "[]", new CodeFormattingOptions() { SpacesWithinEmptyListExpression = true }, "[ ]");
            TestOneString(PythonLanguageVersion.V27, "[ ]", new CodeFormattingOptions() { SpacesWithinEmptyListExpression = true }, "[ ]");
            TestOneString(PythonLanguageVersion.V27, "[]", new CodeFormattingOptions() { SpacesWithinEmptyListExpression = false }, "[]");
            TestOneString(PythonLanguageVersion.V27, "[ ]", new CodeFormattingOptions() { SpacesWithinEmptyListExpression = false }, "[]");
            TestOneString(PythonLanguageVersion.V27, "[]", new CodeFormattingOptions() { SpacesWithinEmptyListExpression = null }, "[]");
            TestOneString(PythonLanguageVersion.V27, "[ ]", new CodeFormattingOptions() { SpacesWithinEmptyListExpression = null }, "[ ]");

            // WithinListExpression
            TestOneString(PythonLanguageVersion.V27, "[a,]", new CodeFormattingOptions() { SpacesWithinListExpression = true }, "[ a, ]");
            TestOneString(PythonLanguageVersion.V27, "[a,b]", new CodeFormattingOptions() { SpacesWithinListExpression = true }, "[ a,b ]");
            TestOneString(PythonLanguageVersion.V27, "[ a, ]", new CodeFormattingOptions() { SpacesWithinListExpression = true }, "[ a, ]");
            TestOneString(PythonLanguageVersion.V27, "[ a,b ]", new CodeFormattingOptions() { SpacesWithinListExpression = true }, "[ a,b ]");
            TestOneString(PythonLanguageVersion.V27, "[a,]", new CodeFormattingOptions() { SpacesWithinListExpression = false }, "[a,]");
            TestOneString(PythonLanguageVersion.V27, "[a,b]", new CodeFormattingOptions() { SpacesWithinListExpression = false }, "[a,b]");
            TestOneString(PythonLanguageVersion.V27, "[ a, ]", new CodeFormattingOptions() { SpacesWithinListExpression = false }, "[a,]");
            TestOneString(PythonLanguageVersion.V27, "[ a,b ]", new CodeFormattingOptions() { SpacesWithinListExpression = false }, "[a,b]");
            TestOneString(PythonLanguageVersion.V27, "[a,]", new CodeFormattingOptions() { SpacesWithinListExpression = null }, "[a,]");
            TestOneString(PythonLanguageVersion.V27, "[a,b]", new CodeFormattingOptions() { SpacesWithinListExpression = null }, "[a,b]");
            TestOneString(PythonLanguageVersion.V27, "[ a, ]", new CodeFormattingOptions() { SpacesWithinListExpression = null }, "[ a, ]");
            TestOneString(PythonLanguageVersion.V27, "[ a,b ]", new CodeFormattingOptions() { SpacesWithinListExpression = null }, "[ a,b ]");

            // SpacesAroundBinaryOperators
            foreach (var op in new[] { "+", "-", "/", "//", "*", "%", "**", "<<", ">>", "&", "|", "^", "<", ">", "<=", ">=", "!=", "<>" }) {
                TestOneString(PythonLanguageVersion.V27, "aa " + op + " bb", new CodeFormattingOptions() { SpacesAroundBinaryOperators = true }, "aa " + op + " bb");
                TestOneString(PythonLanguageVersion.V27, "aa" + op + "bb", new CodeFormattingOptions() { SpacesAroundBinaryOperators = true }, "aa " + op + " bb");
                TestOneString(PythonLanguageVersion.V27, "aa  " + op + "  bb", new CodeFormattingOptions() { SpacesAroundBinaryOperators = true }, "aa " + op + " bb");
                TestOneString(PythonLanguageVersion.V27, "aa " + op + " bb", new CodeFormattingOptions() { SpacesAroundBinaryOperators = false }, "aa" + op + "bb");
                TestOneString(PythonLanguageVersion.V27, "aa" + op + "bb", new CodeFormattingOptions() { SpacesAroundBinaryOperators = false }, "aa" + op + "bb");
                TestOneString(PythonLanguageVersion.V27, "aa  " + op + "  bb", new CodeFormattingOptions() { SpacesAroundBinaryOperators = false }, "aa" + op + "bb");
                TestOneString(PythonLanguageVersion.V27, "aa " + op + " bb", new CodeFormattingOptions() { SpacesAroundBinaryOperators = null }, "aa " + op + " bb");
                TestOneString(PythonLanguageVersion.V27, "aa" + op + "bb", new CodeFormattingOptions() { SpacesAroundBinaryOperators = null }, "aa" + op + "bb");
                TestOneString(PythonLanguageVersion.V27, "aa  " + op + "  bb", new CodeFormattingOptions() { SpacesAroundBinaryOperators = null }, "aa  " + op + "  bb");
            }

            foreach (var op in new[] { "is", "in", "is not", "not in" }) {
                TestOneString(PythonLanguageVersion.V27, "aa " + op + " bb", new CodeFormattingOptions() { SpacesAroundBinaryOperators = true }, "aa " + op + " bb");
                TestOneString(PythonLanguageVersion.V27, "aa  " + op + "  bb", new CodeFormattingOptions() { SpacesAroundBinaryOperators = true }, "aa " + op + " bb");
                TestOneString(PythonLanguageVersion.V27, "aa " + op + " bb", new CodeFormattingOptions() { SpacesAroundBinaryOperators = false }, "aa " + op + " bb");
                TestOneString(PythonLanguageVersion.V27, "aa  " + op + "  bb", new CodeFormattingOptions() { SpacesAroundBinaryOperators = false }, "aa " + op + " bb");
                TestOneString(PythonLanguageVersion.V27, "aa " + op + " bb", new CodeFormattingOptions() { SpacesAroundBinaryOperators = null }, "aa " + op + " bb");
                TestOneString(PythonLanguageVersion.V27, "aa  " + op + "  bb", new CodeFormattingOptions() { SpacesAroundBinaryOperators = null }, "aa  " + op + "  bb");
            }

            // SpacesAroundAssignmentOperator
            TestOneString(PythonLanguageVersion.V27, "x = 2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = true }, "x = 2");
            TestOneString(PythonLanguageVersion.V27, "x=2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = true }, "x = 2");
            TestOneString(PythonLanguageVersion.V27, "x  =  2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = true }, "x = 2");
            TestOneString(PythonLanguageVersion.V27, "x = y = 2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = true }, "x = y = 2");
            TestOneString(PythonLanguageVersion.V27, "x=y=2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = true }, "x = y = 2");
            TestOneString(PythonLanguageVersion.V27, "x  =  y  =  2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = true }, "x = y = 2");

            TestOneString(PythonLanguageVersion.V27, "x = 2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = false }, "x=2");
            TestOneString(PythonLanguageVersion.V27, "x=2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = false }, "x=2");
            TestOneString(PythonLanguageVersion.V27, "x  =  2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = false }, "x=2");
            TestOneString(PythonLanguageVersion.V27, "x = y = 2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = false }, "x=y=2");
            TestOneString(PythonLanguageVersion.V27, "x=y=2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = false }, "x=y=2");
            TestOneString(PythonLanguageVersion.V27, "x  =  y  =  2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = false }, "x=y=2");

            TestOneString(PythonLanguageVersion.V27, "x = 2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = null }, "x = 2");
            TestOneString(PythonLanguageVersion.V27, "x=2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = null }, "x=2");
            TestOneString(PythonLanguageVersion.V27, "x  =  2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = null }, "x  =  2");
            TestOneString(PythonLanguageVersion.V27, "x = y = 2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = null }, "x = y = 2");
            TestOneString(PythonLanguageVersion.V27, "x=y=2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = null }, "x=y=2");
            TestOneString(PythonLanguageVersion.V27, "x  =  y  =  2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = null }, "x  =  y  =  2");

            /* Statements */
            // ReplaceMultipleImportsWithMultipleStatements
            TestOneString(PythonLanguageVersion.V27, "import fob", new CodeFormattingOptions() { ReplaceMultipleImportsWithMultipleStatements = true }, "import fob");
            TestOneString(PythonLanguageVersion.V27, "import fob, oar", new CodeFormattingOptions() { ReplaceMultipleImportsWithMultipleStatements = true }, $"import fob{Environment.NewLine}import oar");
            TestOneString(PythonLanguageVersion.V27, $"{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}import fob, oar", new CodeFormattingOptions() { ReplaceMultipleImportsWithMultipleStatements = true }, $"{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}import fob{Environment.NewLine}import oar");
            TestOneString(PythonLanguageVersion.V27, $"def f():{Environment.NewLine}    import fob, oar", new CodeFormattingOptions() { ReplaceMultipleImportsWithMultipleStatements = true }, $"def f():{Environment.NewLine}    import fob{Environment.NewLine}    import oar");
            TestOneString(PythonLanguageVersion.V27, "import fob as quox, oar", new CodeFormattingOptions() { ReplaceMultipleImportsWithMultipleStatements = true }, $"import fob as quox{Environment.NewLine}import oar");
            TestOneString(PythonLanguageVersion.V27, "import   fob,  oar", new CodeFormattingOptions() { ReplaceMultipleImportsWithMultipleStatements = true }, $"import   fob{Environment.NewLine}import  oar");
            TestOneString(PythonLanguageVersion.V27, "import fob  as  quox, oar", new CodeFormattingOptions() { ReplaceMultipleImportsWithMultipleStatements = true }, $"import fob  as  quox{Environment.NewLine}import oar");

            TestOneString(PythonLanguageVersion.V27, "import fob", new CodeFormattingOptions() { ReplaceMultipleImportsWithMultipleStatements = false }, "import fob");
            TestOneString(PythonLanguageVersion.V27, "import fob, oar", new CodeFormattingOptions() { ReplaceMultipleImportsWithMultipleStatements = false }, "import fob, oar");
            TestOneString(PythonLanguageVersion.V27, $"{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}import fob, oar", new CodeFormattingOptions() { ReplaceMultipleImportsWithMultipleStatements = false }, $"{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}import fob, oar");
            TestOneString(PythonLanguageVersion.V27, $"def f():{Environment.NewLine}    import fob, oar", new CodeFormattingOptions() { ReplaceMultipleImportsWithMultipleStatements = false }, $"def f():{Environment.NewLine}    import fob, oar");
            TestOneString(PythonLanguageVersion.V27, "import fob as quox, oar", new CodeFormattingOptions() { ReplaceMultipleImportsWithMultipleStatements = false }, "import fob as quox, oar");
            TestOneString(PythonLanguageVersion.V27, "import   fob,  oar", new CodeFormattingOptions() { ReplaceMultipleImportsWithMultipleStatements = false }, "import   fob,  oar");
            TestOneString(PythonLanguageVersion.V27, "import fob  as  quox, oar", new CodeFormattingOptions() { ReplaceMultipleImportsWithMultipleStatements = false }, "import fob  as  quox, oar");

            // RemoveTrailingSemicolons
            TestOneString(PythonLanguageVersion.V27, "x = 42;", new CodeFormattingOptions() { RemoveTrailingSemicolons = true }, "x = 42");
            TestOneString(PythonLanguageVersion.V27, "x = 42  ;", new CodeFormattingOptions() { RemoveTrailingSemicolons = true }, "x = 42");
            TestOneString(PythonLanguageVersion.V27, "x = 42;  y = 100;", new CodeFormattingOptions() { RemoveTrailingSemicolons = true }, "x = 42;  y = 100");
            TestOneString(PythonLanguageVersion.V27, "x = 42;", new CodeFormattingOptions() { RemoveTrailingSemicolons = false }, "x = 42;");
            TestOneString(PythonLanguageVersion.V27, "x = 42  ;", new CodeFormattingOptions() { RemoveTrailingSemicolons = false }, "x = 42  ;");
            TestOneString(PythonLanguageVersion.V27, "x = 42;  y = 100;", new CodeFormattingOptions() { RemoveTrailingSemicolons = false }, "x = 42;  y = 100;");

            // BreakMultipleStatementsPerLine
            TestOneString(PythonLanguageVersion.V27, "x = 42; y = 100", new CodeFormattingOptions() { BreakMultipleStatementsPerLine = true }, $"x = 42{Environment.NewLine}y = 100");
            TestOneString(PythonLanguageVersion.V27, $"def f():{Environment.NewLine}    x = 42; y = 100", new CodeFormattingOptions() { BreakMultipleStatementsPerLine = true }, $"def f():{Environment.NewLine}    x = 42{Environment.NewLine}    y = 100");
            TestOneString(PythonLanguageVersion.V27, "x = 42; y = 100;", new CodeFormattingOptions() { BreakMultipleStatementsPerLine = true }, $"x = 42{Environment.NewLine}y = 100;");
            TestOneString(PythonLanguageVersion.V27, $"def f():{Environment.NewLine}    x = 42; y = 100;", new CodeFormattingOptions() { BreakMultipleStatementsPerLine = true }, $"def f():{Environment.NewLine}    x = 42{Environment.NewLine}    y = 100;");
            TestOneString(PythonLanguageVersion.V27, "x = 42; y = 100", new CodeFormattingOptions() { BreakMultipleStatementsPerLine = true, RemoveTrailingSemicolons = true }, $"x = 42{Environment.NewLine}y = 100");
            TestOneString(PythonLanguageVersion.V27, $"def f():{Environment.NewLine}    x = 42; y = 100", new CodeFormattingOptions() { BreakMultipleStatementsPerLine = true, RemoveTrailingSemicolons = true }, $"def f():{Environment.NewLine}    x = 42{Environment.NewLine}    y = 100");
        }

        [TestMethod, Priority(1)]
        public void TestReflowComment() {
            var commentTestCases = new[] { 
                new {
                    Before = $"  # Beautiful is better than ugly. Explicit is better than implicit. Simple is better than complex. Complex is better than complicated.{Environment.NewLine}",
                    After =  $"  # Beautiful is better than ugly.  Explicit is better than implicit.  Simple{Environment.NewLine}  # is better than complex.  Complex is better than complicated.{Environment.NewLine}"
                },
                new { 
                    Before = $"## Beautiful is better than ugly. Explicit is better than implicit. Simple is better than complex. Complex is better than complicated.{Environment.NewLine}",
                    After =  $"## Beautiful is better than ugly.  Explicit is better than implicit.  Simple is{Environment.NewLine}## better than complex.  Complex is better than complicated.{Environment.NewLine}"
                },
                new {
                    Before = $"############# Beautiful is better than ugly. Explicit is better than implicit. Simple is better than complex. Complex is better than complicated.{Environment.NewLine}",
                    After =  $"############# Beautiful is better than ugly.  Explicit is better than implicit.{Environment.NewLine}############# Simple is better than complex.  Complex is better than{Environment.NewLine}############# complicated.{Environment.NewLine}"
                },
                new {
                    Before = $"  # Beautiful is better than ugly.{Environment.NewLine}  # import fob{Environment.NewLine}  # Explicit is better than implicit. Simple is better than complex. Complex is better than complicated.{Environment.NewLine}",
                    After =  $"  # Beautiful is better than ugly.{Environment.NewLine}  # import fob{Environment.NewLine}  # Explicit is better than implicit.  Simple is better than complex.  Complex{Environment.NewLine}  # is better than complicated.{Environment.NewLine}"
                },
                new {
                    Before = $"  #{Environment.NewLine}  #   Beautiful is better than ugly.{Environment.NewLine}  #   import fob{Environment.NewLine}  #   Explicit is better than implicit. Simple is better than complex. Complex is better than complicated.{Environment.NewLine}",
                    After =  $"  #{Environment.NewLine}  #   Beautiful is better than ugly.{Environment.NewLine}  #   import fob{Environment.NewLine}  #   Explicit is better than implicit.  Simple is better than complex.{Environment.NewLine}  #   Complex is better than complicated.{Environment.NewLine}"
                },
                new {
                    Before = @"def fob ( ):
    # 12345678901234567890123456789012345678901234567890123456789012345678901234567890
    print 'fob'",
                    After =  @"def fob ( ):
    # 12345678901234567890123456789012345678901234567890123456789012345678901234567890
    print 'fob'",
                },
                new {
                    Before = @"def fob ( ):
    # 12345678901234567890123456789012345678901234567890123456789012345678901234567890          
    print 'fob'",
                    After =  @"def fob ( ):
    # 12345678901234567890123456789012345678901234567890123456789012345678901234567890
    print 'fob'",
                },
                new {
                    Before = @"def f ( ):
    # foooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo
    pass

def f ( ):
    # fooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo
    pass

def f ( ):
    # foooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo
    pass",
                    After =  @"def f ( ):
    # foooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo
    pass

def f ( ):
    # fooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo
    pass

def f ( ):
    # foooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo
    pass",
                }
            };

            foreach (var preceedingText in commentTestCases) {
                Console.WriteLine("----");
                Console.WriteLine(preceedingText.Before);

                var allSnippets =
                    TestExpressions.Snippets2x.Select(text => new { Text = text, Version = PythonLanguageVersion.V27 }).Concat(
                    TestExpressions.Statements3x.Select(text => new { Text = text, Version = PythonLanguageVersion.V33 }));

                foreach (var testCase in allSnippets) {
                    Console.WriteLine(testCase);

                    TestOneString(
                        testCase.Version,
                        preceedingText.Before + testCase.Text,
                        new CodeFormattingOptions() { WrapComments = true, WrappingWidth = 80 },
                        preceedingText.After + testCase.Text
                    );
                }
            }

            // TODO: Comments inside of various groupings (base classes, etc...)
            foreach (var preceedingText in commentTestCases) {
                Console.WriteLine("----");
                Console.WriteLine(preceedingText.Before);

                foreach (var testCase in _insertionSnippets) {
                    Console.WriteLine(testCase);

                    var input = testCase.Replace("[INSERT]", preceedingText.Before);
                    var output = testCase.Replace("[INSERT]", preceedingText.After);

                    TestOneString(
                        PythonLanguageVersion.V27,
                        input,
                        new CodeFormattingOptions() { WrapComments = true, WrappingWidth = 80 },
                        output
                    );
                }
            }
        }

        [TestMethod, Priority(1)]
        public void TestReflowComment2() {
            foreach (var optionValue in new bool?[] { true, false, null }) {
                var options = new CodeFormattingOptions() {
                    SpaceWithinClassDeclarationParens = optionValue,
                    SpaceWithinEmptyBaseClassList = optionValue,
                    SpaceWithinFunctionDeclarationParens = optionValue,
                    SpaceWithinEmptyParameterList = optionValue,
                    SpaceAroundDefaultValueEquals = optionValue,
                    SpaceBeforeCallParen = optionValue,
                    SpaceWithinEmptyCallArgumentList = optionValue,
                    SpaceWithinCallParens = optionValue,
                    SpacesWithinParenthesisExpression = optionValue,
                    SpaceWithinEmptyTupleExpression = optionValue,
                    SpacesWithinParenthesisedTupleExpression = optionValue,
                    SpacesWithinEmptyListExpression = optionValue,
                    SpacesWithinListExpression = optionValue,
                    SpaceBeforeIndexBracket = optionValue,
                    SpaceWithinIndexBrackets = optionValue,
                    SpacesAroundBinaryOperators = optionValue,
                    SpacesAroundAssignmentOperator = optionValue,
                };

                foreach (var testCase in _commentInsertionSnippets) {
                    Console.WriteLine(testCase);

                    var parser = Parser.CreateParser(
                        new StringReader(testCase.Replace("[INSERT]", "# comment here")), 
                        PythonLanguageVersion.V27, 
                        new ParserOptions() { Verbatim = true }
                    );
                    var ast = parser.ParseFile();
                    var newCode = ast.ToCodeString(ast, options);
                    Console.WriteLine(newCode);
                    Assert.IsTrue(newCode.IndexOf("# comment here") != -1);
                }
            }
        }

         /// <summary>
        /// Verify trailing \ at the end a file round trips
        /// </summary>
        [TestMethod, Priority(1)]
        public void TestBackslashThenEof() {
            var code = @"x = 100
\";

            var parser = Parser.CreateParser(
                new StringReader(code),
                PythonLanguageVersion.V27,
                new ParserOptions() { Verbatim = true }
            );

            var ast = parser.ParseFile();
            var newCode = ast.ToCodeString(ast);
            Assert.AreEqual(code, newCode);
        }

        /// <summary>
        /// Verify trailing \ doesn't mess up comments
        /// </summary>
        [TestMethod, Priority(1)]
        public void TestReflowComment3() {
            var code = @"def f():
    if a and \
        b:
            print('hi')";

            var parser = Parser.CreateParser(
                new StringReader(code),
                PythonLanguageVersion.V27,
                new ParserOptions() { Verbatim = true }
            );

            var ast = parser.ParseFile();
            var newCode = ast.ToCodeString(ast, new CodeFormattingOptions() { WrapComments = true, WrappingWidth = 20 });
            Assert.AreEqual(newCode, code);
        }

        /// <summary>
        /// Verify reflowing comment doesn't introduce extra new line
        /// </summary>
        [TestMethod, Priority(1)]
        public void TestReflowComment4() {
            var code = @"def f(): # fob
    pass";

            var parser = Parser.CreateParser(
                new StringReader(code),
                PythonLanguageVersion.V27,
                new ParserOptions() { Verbatim = true }
            );

            var ast = parser.ParseFile();
            var newCode = ast.ToCodeString(ast, new CodeFormattingOptions() { WrapComments = true, WrappingWidth = 20 });
            Assert.AreEqual(newCode, code);
        }

        static readonly string[] _commentInsertionSnippets = new[] {
            $"class C(a, [INSERT]{Environment.NewLine}    b): pass",
            $"class C( [INSERT]{Environment.NewLine}    ): pass", 
            $"def f(a, [INSERT]{Environment.NewLine}    b): pass",
            $"def f( [INSERT]{Environment.NewLine}    ): pass", 
            $"def f(a = [INSERT]{Environment.NewLine}    42): pass",
            $"g( f [INSERT]{Environment.NewLine}    (42))",
            $"f( [INSERT]{Environment.NewLine}    )",
            $"f( a, [INSERT]{Environment.NewLine}     )",
            $"f([INSERT]{Environment.NewLine}   a)",
            $"([INSERT]{Environment.NewLine}    a)",
            $"(a [INSERT]{Environment.NewLine}    )",
            $"({Environment.NewLine}    [INSERT]{Environment.NewLine})",
            $"([INSERT]{Environment.NewLine} 1, 2, 3)",
            $"(1,2,3[INSERT]{Environment.NewLine})",
            $"[[INSERT]{Environment.NewLine}]",
            $"[[INSERT]{Environment.NewLine}1,2,3]",
            $"[1,2,3{Environment.NewLine}[INSERT]{Environment.NewLine}]",
            $"(x [INSERT]{Environment.NewLine}[42])",
            $"x[[INSERT]{Environment.NewLine}42]",
            $"x[42{Environment.NewLine}[INSERT]{Environment.NewLine}]",
            $"(a +[INSERT]{Environment.NewLine}b)",
            $"(a[INSERT]{Environment.NewLine}+b)",
        };

        static readonly string[] _insertionSnippets = new[] {
            $"if True:{Environment.NewLine}    pass{Environment.NewLine}[INSERT]else:{Environment.NewLine}    pass",
            $"if True:{Environment.NewLine}    pass{Environment.NewLine}[INSERT]elif True:{Environment.NewLine}    pass",
            $"try:{Environment.NewLine}    pass{Environment.NewLine}[INSERT]finally:{Environment.NewLine}    pass",
            $"try:{Environment.NewLine}    pass{Environment.NewLine}[INSERT]except:{Environment.NewLine}    pass",
            $"try:{Environment.NewLine}    pass{Environment.NewLine}[INSERT]except Exception:{Environment.NewLine}    pass",
            $"try:{Environment.NewLine}    pass{Environment.NewLine}except Exception:{Environment.NewLine}    pass{Environment.NewLine}[INSERT]else:{Environment.NewLine}    pass",
            $"while True:{Environment.NewLine}    pass{Environment.NewLine}[INSERT]else:{Environment.NewLine}    pass",
            $"for x in [1,2,3]:{Environment.NewLine}    pass{Environment.NewLine}[INSERT]else:{Environment.NewLine}    pass",
            /*@"(1, [INSERT]
               2,
               3)"*/
        };


        /// <summary>
        /// Verifies that the proceeding white space is consistent across all nodes.
        /// </summary>
        [TestMethod, Priority(1)]
        public void TestStartWhiteSpace() {
            foreach (var preceedingText in new[] { $"#fob{Environment.NewLine}" }) {
                var allSnippets = 
                    TestExpressions.Snippets2x.Select(text => new { Text = text, Version = PythonLanguageVersion.V27 }).Concat(
                    TestExpressions.Statements3x.Select(text => new { Text = text, Version = PythonLanguageVersion.V33 }));
                
                foreach (var testCase in allSnippets) {
                    var exprText = testCase.Text;
                    var code = preceedingText + exprText;
                    Console.WriteLine(code);

                    var parser = Parser.CreateParser(new StringReader(code), testCase.Version, new ParserOptions() { Verbatim = true });
                    var ast = parser.ParseFile();
                    var stmt = ((SuiteStatement)ast.Body).Statements[0];
                    if (stmt is ExpressionStatement) {
                        var expr = ((ExpressionStatement)stmt).Expression;

                        Assert.AreEqual(preceedingText.Length, expr.StartIndex);
                        Assert.AreEqual(preceedingText.Length + exprText.Length, expr.EndIndex);
                        Assert.AreEqual(preceedingText, expr.GetLeadingWhiteSpace(ast));
                    } else {
                        Assert.AreEqual(preceedingText.Length, stmt.StartIndex);
                        Assert.AreEqual(preceedingText.Length + exprText.Length, stmt.EndIndex);
                        Assert.AreEqual(preceedingText, stmt.GetLeadingWhiteSpace(ast));
                    }
                }
            }
        }

        [TestMethod, Priority(1)]
        public void ExpressionsTest() {            
            // TODO: Trailing white space tests
            // Unary Expressions
            TestOneString(PythonLanguageVersion.V27, "x=~42");
            TestOneString(PythonLanguageVersion.V27, "x=-42");
            TestOneString(PythonLanguageVersion.V27, "x=+42");
            TestOneString(PythonLanguageVersion.V27, "x=not 42");
            TestOneString(PythonLanguageVersion.V27, "x  =   ~    42");
            TestOneString(PythonLanguageVersion.V27, "x  =   -    42");
            TestOneString(PythonLanguageVersion.V27, "x  =   +    42");
            TestOneString(PythonLanguageVersion.V27, "x  =   not    42");

            // Constant Expressions
            TestOneString(PythonLanguageVersion.V27, $"{Environment.NewLine}42");
            TestOneString(PythonLanguageVersion.V27, "42");
            TestOneString(PythonLanguageVersion.V27, "'abc'");
            TestOneString(PythonLanguageVersion.V27, "\"abc\"");
            TestOneString(PythonLanguageVersion.V27, "'''abc'''");
            TestOneString(PythonLanguageVersion.V27, "\"\"\"abc\"\"\"");
            TestOneString(PythonLanguageVersion.V27, "x = - 1");
            TestOneString(PythonLanguageVersion.V27, "x = -1");
            TestOneString(PythonLanguageVersion.V27, "x = - 2147483648");
            TestOneString(PythonLanguageVersion.V27, "x = -2147483648");

            // Conditional Expressions
            TestOneString(PythonLanguageVersion.V27, "1 if True else 2");
            TestOneString(PythonLanguageVersion.V27, "1  if   True    else     2");

            // Generator expressions
            TestOneString(PythonLanguageVersion.V27, "(x for x in abc)");
            TestOneString(PythonLanguageVersion.V27, "(x for x in abc if abc >= 42)");
            TestOneString(PythonLanguageVersion.V27, " (  x   for    x     in      abc       )");
            TestOneString(PythonLanguageVersion.V27, " (  x   for    x     in      abc       if        abc        >=          42          )");
            TestOneString(PythonLanguageVersion.V27, "f(x for x in abc)");
            TestOneString(PythonLanguageVersion.V27, "f(x for x in abc if abc >= 42)");
            TestOneString(PythonLanguageVersion.V27, "f (  x   for    x     in      abc       )");
            TestOneString(PythonLanguageVersion.V27, "f (  x   for    x     in      abc       if        abc        >=          42          )");
            TestOneString(PythonLanguageVersion.V27, "x(a for a,b in x)");
            TestOneString(PythonLanguageVersion.V27, "x  (   a    for     a      ,       b        in        x          )");

            // Lambda Expressions
            TestOneString(PythonLanguageVersion.V27, "lambda x: x");
            TestOneString(PythonLanguageVersion.V27, "lambda x, y: x, y");
            TestOneString(PythonLanguageVersion.V27, "lambda x = 42: x");
            TestOneString(PythonLanguageVersion.V27, "lambda *x: x");
            TestOneString(PythonLanguageVersion.V27, "lambda **x: x");
            TestOneString(PythonLanguageVersion.V27, "lambda *x, **y: x");
            TestOneString(PythonLanguageVersion.V27, "lambda : 42");
            TestOneString(PythonLanguageVersion.V30, "lambda *, x: x");
            TestOneString(PythonLanguageVersion.V27, "lambda  x   :    x");
            TestOneString(PythonLanguageVersion.V27, "lambda  x   ,    y     :      x       ,        y");
            TestOneString(PythonLanguageVersion.V27, "lambda  x   =    42     :      x");
            TestOneString(PythonLanguageVersion.V27, "lambda  *   x    :     x");
            TestOneString(PythonLanguageVersion.V27, "lambda  **   x    :     x");
            TestOneString(PythonLanguageVersion.V27, "lambda  *   x    ,     **      y       :        x");
            TestOneString(PythonLanguageVersion.V27, "lambda  :   42");
            TestOneString(PythonLanguageVersion.V27, "lambda  :   (yield)");
            TestOneString(PythonLanguageVersion.V27, "lambda  :   (yield");
            TestOneString(PythonLanguageVersion.V30, "lambda  *   ,    x     :      x");

            // List Comprehensions
            TestOneString(PythonLanguageVersion.V27, "[x for x in abc]");
            TestOneString(PythonLanguageVersion.V27, "[x for x in abc, baz]");
            TestOneString(PythonLanguageVersion.V27, "[x for x in (abc, baz)]");
            TestOneString(PythonLanguageVersion.V27, "[x for x in abc if abc >= 42]");
            TestOneString(PythonLanguageVersion.V27, " [  x   for    x     in      abc       ]");
            TestOneString(PythonLanguageVersion.V27, " [  x   for    x     in      abc       if        abc        >=          42          ]");
            TestOneString(PythonLanguageVersion.V27, "[v for k,v in x]");
            TestOneString(PythonLanguageVersion.V27, "  [v   for    k     ,      v       in        x         ]");
            TestOneString(PythonLanguageVersion.V27, "[v for (k,v) in x]");
            TestOneString(PythonLanguageVersion.V27, "  [   v    for     (      k       ,        v          )          in           x             ]");

            // Set comprehensions
            TestOneString(PythonLanguageVersion.V27, "{x for x in abc}");
            TestOneString(PythonLanguageVersion.V27, "{x for x in abc if abc >= 42}");
            TestOneString(PythonLanguageVersion.V27, " {  x   for    x     in      abc       }");
            TestOneString(PythonLanguageVersion.V27, " {  x   for    x     in      abc       if        abc        >=          42          }");

            // Dict Comprehensions
            TestOneString(PythonLanguageVersion.V27, "{x:x for x in abc}");
            TestOneString(PythonLanguageVersion.V27, "{x:x for x in abc if abc >= 42}");
            TestOneString(PythonLanguageVersion.V27, " {  x        :         x   for    x     in      abc       }");
            TestOneString(PythonLanguageVersion.V27, " {  x           :            x   for    x     in      abc       if        abc        >=          42          }");

            // Backquote Expression
            TestOneString(PythonLanguageVersion.V27, "`42`");
            TestOneString(PythonLanguageVersion.V27, " `42`");
            TestOneString(PythonLanguageVersion.V27, " `42  `");

            // Call Expression
            TestOneString(PythonLanguageVersion.V27, "x(abc)");
            TestOneString(PythonLanguageVersion.V27, "x(abc = 42)");
            TestOneString(PythonLanguageVersion.V27, "x(*abc)");
            TestOneString(PythonLanguageVersion.V27, "x(**abc)");
            TestOneString(PythonLanguageVersion.V27, "x(*fob, **oar)");
            TestOneString(PythonLanguageVersion.V27, "x(a, b, c)");
            TestOneString(PythonLanguageVersion.V27, "x(a, b, c, d = 42)");
            TestOneString(PythonLanguageVersion.V27, "x (  abc   )");
            TestOneString(PythonLanguageVersion.V27, "x (  abc   =    42     )");
            TestOneString(PythonLanguageVersion.V27, "x (  *   abc    )");
            TestOneString(PythonLanguageVersion.V27, "x (  **   abc     )");
            TestOneString(PythonLanguageVersion.V27, "x (  *   fob    ,     **      oar       )");
            TestOneString(PythonLanguageVersion.V27, "x (  a,   b,    c     )");
            TestOneString(PythonLanguageVersion.V27, "x (  a   ,    b     ,      c       ,        d         =           42           )");
            TestOneString(PythonLanguageVersion.V27, "x(abc,)");
            TestOneString(PythonLanguageVersion.V27, "x  (   abc    ,     )");
            TestOneString(PythonLanguageVersion.V27, "x(abc=42,)");
            TestOneString(PythonLanguageVersion.V27, "x  (   abc    =     42      ,       )");

            // Member Expression
            TestOneString(PythonLanguageVersion.V27, "fob.oar");
            TestOneString(PythonLanguageVersion.V27, "fob .oar");
            TestOneString(PythonLanguageVersion.V27, "fob. oar");
            TestOneString(PythonLanguageVersion.V27, "fob .  oar");
            TestOneString(PythonLanguageVersion.V27, $"class C:{Environment.NewLine}    x = fob.__oar");

            // Parenthesis expression
            TestOneString(PythonLanguageVersion.V27, "(42)");
            TestOneString(PythonLanguageVersion.V27, "( 42  )");
            TestOneString(PythonLanguageVersion.V27, " (  42   )");

            // Starred expression
            TestOneString(PythonLanguageVersion.V30, "*a, b = c, d");
            TestOneString(PythonLanguageVersion.V30, "*a, b, c = d, e, f");
            TestOneString(PythonLanguageVersion.V30, "*               a ,  b   ,    c     =      d       ,        e         ,          f");
            TestOneString(PythonLanguageVersion.V30, "(            *               a ,  b   ,    c     )             =      (              d       ,        e         ,          f              )");
            TestOneString(PythonLanguageVersion.V30, "[            *               a ,  b   ,    c     ]             =      [              d       ,        e         ,          f              ]");
            
            // Index expression
            TestOneString(PythonLanguageVersion.V27, "x[42]");
            TestOneString(PythonLanguageVersion.V27, "x[ 42]");
            
            TestOneString(PythonLanguageVersion.V27, "x [42]");
            TestOneString(PythonLanguageVersion.V27, "x [42 ]");
            TestOneString(PythonLanguageVersion.V27, "x [42,23]");
            TestOneString(PythonLanguageVersion.V27, "x[ 42 ]");
            TestOneString(PythonLanguageVersion.V27, "x [  42   ,    23     ]");
            TestOneString(PythonLanguageVersion.V27, "x[42:23]");
            TestOneString(PythonLanguageVersion.V27, "x [  42   :    23     ]");
            TestOneString(PythonLanguageVersion.V27, "x[42:23:100]");
            TestOneString(PythonLanguageVersion.V27, "x [  42   :    23     :      100       ]");
            TestOneString(PythonLanguageVersion.V27, "x[42:]");
            TestOneString(PythonLanguageVersion.V27, "x[42::]");
            TestOneString(PythonLanguageVersion.V27, "x [  42   :    ]");
            TestOneString(PythonLanguageVersion.V27, "x [  42   :    :     ]");
            TestOneString(PythonLanguageVersion.V27, "x[::]");
            TestOneString(PythonLanguageVersion.V27, "x  [   :    :     ]");

            // or expression
            TestOneString(PythonLanguageVersion.V27, "1 or 2");
            TestOneString(PythonLanguageVersion.V27, "1  or   2");

            // and expression
            TestOneString(PythonLanguageVersion.V27, "1 and 2");
            TestOneString(PythonLanguageVersion.V27, "1  and   2");

            // binary expression
            foreach (var op in new[] { "+", "-", "*", "/", "//", "%", "&", "|", "^", "<<", ">>", "**", "<", ">", "<=", ">=", "==", "!=", "<>" }) {
                TestOneString(PythonLanguageVersion.V27, "1 " + op + "2");
                TestOneString(PythonLanguageVersion.V27, "1"+ op + "2");
                TestOneString(PythonLanguageVersion.V27, "1" + op + " 2");
                TestOneString(PythonLanguageVersion.V27, "1 " + op + " 2");
                TestOneString(PythonLanguageVersion.V27, "1  " + op + "   2");
            }

            foreach (var op in new[] { "is", "is not", "in", "not in" }) {
                // TODO: All of these should pass in the binary expression case once we have error handling working
                TestOneString(PythonLanguageVersion.V27, "1 " + op + " 2");
                TestOneString(PythonLanguageVersion.V27, "1  " + op + "   2");
            }

            // yield expression
            TestOneString(PythonLanguageVersion.V27, "yield 1");
            TestOneString(PythonLanguageVersion.V27, "yield 1, 2");
            TestOneString(PythonLanguageVersion.V27, "yield 1  , 2");
            TestOneString(PythonLanguageVersion.V27, "yield 1  , 2,");
            TestOneString(PythonLanguageVersion.V27, "yield 1 ,  2   ,");
            TestOneString(PythonLanguageVersion.V27, "yield");
            TestOneString(PythonLanguageVersion.V27, "yield None");
            TestOneString(PythonLanguageVersion.V27, "yield 1 == 2");
            TestOneString(PythonLanguageVersion.V27, "yield lambda: 42");
            TestOneString(PythonLanguageVersion.V27, "yield 42, ");

            // yield from expression
            TestOneString(PythonLanguageVersion.V33, "yield from fob");
            TestOneString(PythonLanguageVersion.V33, "yield from  fob");
            TestOneString(PythonLanguageVersion.V33, "yield  from fob");
            TestOneString(PythonLanguageVersion.V33, "yield  from  fob");
            TestOneString(PythonLanguageVersion.V33, "x  =  yield  from  fob");

            // tuples
            TestOneString(PythonLanguageVersion.V27, "(1, 2, 3)");
            TestOneString(PythonLanguageVersion.V27, "(1, 2,  3)");
            TestOneString(PythonLanguageVersion.V27, "( 1  ,   2    ,     3      )");
            TestOneString(PythonLanguageVersion.V27, "( 1  ,   2    ,     3      ,       )");
            
            // list expressions
            TestOneString(PythonLanguageVersion.V27, "[1, 2, 3]");
            TestOneString(PythonLanguageVersion.V27, "[1, 2,  3]");
            TestOneString(PythonLanguageVersion.V27, "[ 1  ,   2    ,     3      ]");
            TestOneString(PythonLanguageVersion.V27, "[ 1  ,   2    ,     3      ,       ]");
            TestOneString(PythonLanguageVersion.V27, "[abc, fob and oar]");
            TestOneString(PythonLanguageVersion.V27, "[fob if True else oar]");

            // set expressions
            TestOneString(PythonLanguageVersion.V27, "{1, 2, 3}");
            TestOneString(PythonLanguageVersion.V27, "{1, 2,  3}");
            TestOneString(PythonLanguageVersion.V27, "{ 1  ,   2    ,     3      }");
            TestOneString(PythonLanguageVersion.V27, "{ 1  ,   2    ,     3      ,       }");

            // dict expressions
            TestOneString(PythonLanguageVersion.V27, "{1:2, 2 :3, 3: 4}");
            TestOneString(PythonLanguageVersion.V27, "{1 :2, 2  :3,  3:  4}");
            TestOneString(PythonLanguageVersion.V27, "{ 1  :   2    ,     2      :       3,        3         :          4           }");
            TestOneString(PythonLanguageVersion.V27, "{ 1  :   2    ,     2      :       3,        3         :          4           ,            }");

            // Error cases:
            //TestOneString(PythonLanguageVersion.V27, "{1:2, 2 :3, 3: 4]");
        }

        [TestMethod, Priority(1)]
        public void TestMangledPrivateName() {
            TestOneString(PythonLanguageVersion.V27, @"class C:
    def f(__a):
        pass
"); 
            TestOneString(PythonLanguageVersion.V27, @"class C:
    class __D:
        pass
");


            TestOneString(PythonLanguageVersion.V27, @"class C:
    import __abc
    import __fob, __oar
");

            TestOneString(PythonLanguageVersion.V27, @"class C:
    from sys import __abc
    from sys import __fob, __oar
    from __sys import __abc
");

            TestOneString(PythonLanguageVersion.V27, @"class C:
    global __X
");

            TestOneString(PythonLanguageVersion.V30, @"class C:
    nonlocal __X
");
        }

        [TestMethod, Priority(1)]
        public void TestComments() {

            TestOneString(PythonLanguageVersion.V27, @"x = fob(
        r'abc'                                # comments
        r'def'                                # are spanning across
                                              # a string plus
                                              # which might make life
                                              # difficult if we don't
        r'ghi'                                # handle it properly
        )");

            TestOneString(PythonLanguageVersion.V27, $"#fob{Environment.NewLine}pass");
            TestOneString(PythonLanguageVersion.V27, $"#fob{Environment.NewLine}{Environment.NewLine}pass"); 
            TestOneString(PythonLanguageVersion.V27, "#fob");

        }

        [TestMethod, Priority(1)]
        public void TestWhiteSpaceAfterDocString() {
            TestOneString(PythonLanguageVersion.V27, @"'''hello

this is some documentation
'''

import fob");
        }

        [TestMethod, Priority(1)]
        public void TestBinaryFiles() {
            var filename = TestData.GetPath("TestData", "random.bin");
            var originalText = File.ReadAllText(filename);
            TestOneString(PythonLanguageVersion.V27, originalText);
        }

        [TestMethod, Priority(1)]
        public void TestFStringWithoutVerbatim() {
            TestOneString(PythonLanguageVersion.V36, "f'''sss {1:5}'''", null, null, true, null, false);
        }

        [TestMethod, Priority(1)]
        public void TestErrors() {
            TestOneString(PythonLanguageVersion.V30, ":   ...");

            // Index Expression
            TestOneString(PythonLanguageVersion.V27, "x[[val, val, ...], [val, val, ...], .");
            TestOneString(PythonLanguageVersion.V27, "x[[val, val, ...], [val, val, ...], ..");

            // Suite Statement
            TestOneString(PythonLanguageVersion.V27, $"while X !=2 :{Environment.NewLine}");

            // Lambda Expression

            TestOneString(PythonLanguageVersion.V27, "lambda");
            TestOneString(PythonLanguageVersion.V27, "lambda ");
            TestOneString(PythonLanguageVersion.V27, "lambda :");
            TestOneString(PythonLanguageVersion.V27, "lambda pass");
            TestOneString(PythonLanguageVersion.V27, "lambda : pass"); 
            TestOneString(PythonLanguageVersion.V27, "lambda a, b, quote");
            TestOneString(PythonLanguageVersion.V30, "[x for x in abc if lambda a, b, quote");
            TestOneString(PythonLanguageVersion.V27, "lambda, X+Y Z");
            TestOneString(PythonLanguageVersion.V30, "[x for x in abc if lambda, X+Y Z");

            // print statement
            TestOneString(PythonLanguageVersion.V27, $"print >>sys.stderr, \\{Environment.NewLine}");
            TestOneString(PythonLanguageVersion.V27, "print pass");
            TestOneString(PythonLanguageVersion.V27, "print >>pass");
            TestOneString(PythonLanguageVersion.V27, "print >>pass, ");
            TestOneString(PythonLanguageVersion.V27, "print >>pass, pass");
            TestOneString(PythonLanguageVersion.V27, "print >>pass pass");

            // Import statement
            TestOneString(PythonLanguageVersion.V27, "import X as");

            // From Import statement
            TestOneString(PythonLanguageVersion.V27, "from _struct import");
            TestOneString(PythonLanguageVersion.V27, "from _io import (DEFAULT_BUFFER_SIZE");
            TestOneString(PythonLanguageVersion.V27, "from x import y as");
            TestOneString(PythonLanguageVersion.V27, "from ... import ...");

            // Parenthesis Expression
            TestOneString(PythonLanguageVersion.V27, $"({Environment.NewLine}(x");
            TestOneString(PythonLanguageVersion.V27, $"({Environment.NewLine}(");            

            TestOneString(PythonLanguageVersion.V27, "m .b'");
            TestOneString(PythonLanguageVersion.V27, "m . b'");
            TestOneString(PythonLanguageVersion.V27, "x y import");
            TestOneString(PythonLanguageVersion.V27, "x y global");

            TestOneString(PythonLanguageVersion.V27, "x[..., ]");

            TestOneString(PythonLanguageVersion.V27, "(a for x y");
            TestOneString(PythonLanguageVersion.V27, "x(a for x y");
            TestOneString(PythonLanguageVersion.V27, "[a for x y");
            TestOneString(PythonLanguageVersion.V27, "{a for x y");
            TestOneString(PythonLanguageVersion.V27, "{a:v for x y");

            TestOneString(PythonLanguageVersion.V27, ":   ");
            TestOneString(PythonLanguageVersion.V27, "from the");
            TestOneString(PythonLanguageVersion.V27, "when not None");
            TestOneString(PythonLanguageVersion.V27, "for x and y");

            // conditional expression
            TestOneString(PythonLanguageVersion.V27, "e if x y z");
            TestOneString(PythonLanguageVersion.V27, "e if x y");
            TestOneString(PythonLanguageVersion.V27, "e if x");
            TestOneString(PythonLanguageVersion.V27, "e if x pass");

            TestOneString(PythonLanguageVersion.V27, $", 'hello'{Environment.NewLine}        self");
            TestOneString(PythonLanguageVersion.V27, "http://xkcd.com/353/\")");
            TestOneString(PythonLanguageVersion.V27, "�g�\r��\r���\r��\r���\r���\r��\rt4�\r*V�\roA�\r\t�\r�$�\r\t.�\r�t�\r�q�\r�H�\r�|");
            TestOneString(PythonLanguageVersion.V27, "\r\t.�\r�t�\r�q�\r");
            TestOneString(PythonLanguageVersion.V27, "\r\t�\r�$�\r\t.�\r");
            TestOneString(PythonLanguageVersion.V27, "�\r�$�\r\t.�\r�t");
            TestOneString(PythonLanguageVersion.V27, $"{Environment.NewLine}.{Environment.NewLine}");
            
            TestOneString(PythonLanguageVersion.V27, $"abc{Environment.NewLine}.{Environment.NewLine}");

            // Dictionary Expressions
            TestOneString(PythonLanguageVersion.V27, "{");
            TestOneString(PythonLanguageVersion.V27, @"X = { 42 : 100,
");
            TestOneString(PythonLanguageVersion.V27, @"s.
    X = { 23   : 42,
");
            TestOneString(PythonLanguageVersion.V27, "{x:y");
            TestOneString(PythonLanguageVersion.V27, "{x:y, z:x");
            TestOneString(PythonLanguageVersion.V27, "{x");
            TestOneString(PythonLanguageVersion.V27, "{x, y");
            TestOneString(PythonLanguageVersion.V27, "{x:y for x in abc");
            TestOneString(PythonLanguageVersion.V27, "{x for x in abc");
            TestOneString(PythonLanguageVersion.V27, @")
    X = { 42 : 100,
          100 : 200,
");
            TestOneString(PythonLanguageVersion.V27, @"]
    X = { 42 : 100,
          100 : 200,
");
            TestOneString(PythonLanguageVersion.V27, @"}
    X = { 42 : 100,
          100 : 200,
");
            TestOneString(PythonLanguageVersion.V27, @"{ 42: 100, 100 ");
            TestOneString(PythonLanguageVersion.V27, @"{ 42: 100, 100, 200:30 } ");
            TestOneString(PythonLanguageVersion.V27, @"{ 100, 100:30, 200 } ");


            // generator comprehensions and calls
            TestOneString(PythonLanguageVersion.V27, "x(");
            TestOneString(PythonLanguageVersion.V27, "x(for x in abc");
            TestOneString(PythonLanguageVersion.V27, "x(abc");
            TestOneString(PythonLanguageVersion.V27, "x(abc, ");
            TestOneString(PythonLanguageVersion.V27, "x(pass");

            // lists and list comprehensions
            TestOneString(PythonLanguageVersion.V27, "[");
            TestOneString(PythonLanguageVersion.V27, "[abc");
            TestOneString(PythonLanguageVersion.V27, "[abc,");
            TestOneString(PythonLanguageVersion.V27, "[for x in abc");
            TestOneString(PythonLanguageVersion.V27, "[b for b in");

            TestOneString(PythonLanguageVersion.V27, "x[");
            TestOneString(PythonLanguageVersion.V27, "x[abc");
            TestOneString(PythonLanguageVersion.V27, "x[abc,");
            TestOneString(PythonLanguageVersion.V27, "x[abc:");

            // backquote expression
            TestOneString(PythonLanguageVersion.V27, "`fob");

            // constant expressions
            TestOneString(PythonLanguageVersion.V27, "'\r");
            TestOneString(PythonLanguageVersion.V27, @"'abc' 24 : q");
            TestOneString(PythonLanguageVersion.V27, @"u'abc' 24 : q");

            // bad tokens
            TestOneString(PythonLanguageVersion.V27, "!x");
            TestOneString(PythonLanguageVersion.V27, "$aü");
            TestOneString(PythonLanguageVersion.V27, "0399");
            TestOneString(PythonLanguageVersion.V27, "0o399");
            TestOneString(PythonLanguageVersion.V27, "0399L");
            TestOneString(PythonLanguageVersion.V27, "0399j");
            
            // calls
            TestOneString(PythonLanguageVersion.V27, "x(42 = 42)");

            // for statement
            TestOneString(PythonLanguageVersion.V27, $"for pass{Environment.NewLine}in abc: pass");
            TestOneString(PythonLanguageVersion.V27, "for pass in abc: pass");
            TestOneString(PythonLanguageVersion.V27, $"def f():{Environment.NewLine}abc");
            TestOneString(PythonLanguageVersion.V27, "for pass in");

            // class defs
            TestOneString(PythonLanguageVersion.V30, "class(object: pass");
            TestOneString(PythonLanguageVersion.V30, "class X(object: pass");
            TestOneString(PythonLanguageVersion.V30, "class X(object, int: pass");
            TestOneString(PythonLanguageVersion.V30, "class X(object, pass");
            TestOneString(PythonLanguageVersion.V30, "class X(=");
            TestOneString(PythonLanguageVersion.V30, "class X(pass");

            TestOneString(PythonLanguageVersion.V27, "class(object: pass");
            TestOneString(PythonLanguageVersion.V27, "class X(object: pass");
            TestOneString(PythonLanguageVersion.V27, "class X(object, int: pass");
            TestOneString(PythonLanguageVersion.V27, "class X(object, pass");
            TestOneString(PythonLanguageVersion.V27, "class X(=");
            TestOneString(PythonLanguageVersion.V27, "class X(pass");

            TestOneString(PythonLanguageVersion.V27, $"class C:{Environment.NewLine}    x = fob.42");
            TestOneString(PythonLanguageVersion.V27, $"class C:{Environment.NewLine}    @fob.42{Environment.NewLine}    def f(self): pass");
            TestOneString(PythonLanguageVersion.V27, $"class C:{Environment.NewLine}    @fob.[]{Environment.NewLine}    def f(self): pass");
            TestOneString(PythonLanguageVersion.V27, "class 42");
            TestOneString(PythonLanguageVersion.V30, "class");
            TestOneString(PythonLanguageVersion.V27, $"@fob{Environment.NewLine}class 42");

            // func defs
            TestOneString(PythonLanguageVersion.V30, "def f(A, *, *x");
            TestOneString(PythonLanguageVersion.V30, "def f(A, *, **x");
            TestOneString(PythonLanguageVersion.V30, "def f(A, *, x = 2");
            TestOneString(PythonLanguageVersion.V30, "def f(A, *");
            TestOneString(PythonLanguageVersion.V30, "def f(A, *, (a, b)");
            TestOneString(PythonLanguageVersion.V30, "def f(A, *,");
            TestOneString(PythonLanguageVersion.V30, "def f(A, *)");

            TestOneString(PythonLanguageVersion.V27, "def f(x, *, ): pass");
            TestOneString(PythonLanguageVersion.V27, "def f((42 + 2: pass");
            TestOneString(PythonLanguageVersion.V27, "def f((42: pass");
            TestOneString(PythonLanguageVersion.V27, "def f((42)): pass");
            TestOneString(PythonLanguageVersion.V27, "def f((42, )): pass");
            TestOneString(PythonLanguageVersion.V27, "def f((42): pass");
            TestOneString(PythonLanguageVersion.V27, "def f((42 pass");
            TestOneString(PythonLanguageVersion.V27, "def f((a, 42)): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(42 = 42): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(42 = pass): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(pass = pass): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(= = =): pass");
            TestOneString(PythonLanguageVersion.V27, "def f");
            TestOneString(PythonLanguageVersion.V27, "def");
            TestOneString(PythonLanguageVersion.V27, " @@");
            TestOneString(PythonLanguageVersion.V27, "def X(abc, **");
            TestOneString(PythonLanguageVersion.V27, "def X(abc, *");
            TestOneString(PythonLanguageVersion.V27, @"@fob(
def f(): pass");


            // misc malformed expressions
            TestOneString(PythonLanguageVersion.V27, "1 + :");
            TestOneString(PythonLanguageVersion.V27, "abc.2");
            TestOneString(PythonLanguageVersion.V27, "abc 1L");
            TestOneString(PythonLanguageVersion.V27, "abc 0j");
            TestOneString(PythonLanguageVersion.V27, "abc.2.3");
            TestOneString(PythonLanguageVersion.V27, "abc 1L 2L");
            TestOneString(PythonLanguageVersion.V27, "abc 0j 1j");

            // global / nonlocal statements
            TestOneString(PythonLanguageVersion.V27, "global abc, baz,"); // trailing comma not allowed
            TestOneString(PythonLanguageVersion.V27, "nonlocal abc");           // nonlocal not supported before 3.0
            TestOneString(PythonLanguageVersion.V30, "nonlocal abc, baz,"); // trailing comma not allowed

            // assert statements
            TestOneString(PythonLanguageVersion.V27, "assert");

            // while statements
            TestOneString(PythonLanguageVersion.V27, $"while True:{Environment.NewLine}    break{Environment.NewLine}else:{Environment.NewLine}pass");

            // if statements
            TestOneString(PythonLanguageVersion.V27, $"if True:{Environment.NewLine}    pass{Environment.NewLine}elif False:{Environment.NewLine}    pass{Environment.NewLine}    else:{Environment.NewLine}    pass");

            // try/except
            TestOneString(PythonLanguageVersion.V27, $"try: pass{Environment.NewLine}except: pass{Environment.NewLine}else: pass{Environment.NewLine}except Exception: pass");
            TestOneString(PythonLanguageVersion.V27, "try  :   pass\r\finally    :     pass");

            // Variable annotations
            TestOneString(PythonLanguageVersion.V36, "a:b, c");
            TestOneString(PythonLanguageVersion.V36, "a:b, c = 1");
            TestOneString(PythonLanguageVersion.V36, "a : b, c");
            TestOneString(PythonLanguageVersion.V36, "a : b, c = 1");

            TestOneString(PythonLanguageVersion.V36, "a,b:c");
            TestOneString(PythonLanguageVersion.V36, "a,b:c = 1");
            TestOneString(PythonLanguageVersion.V36, "a , b : c");
            TestOneString(PythonLanguageVersion.V36, "a , b : c = 1");

            TestOneString(PythonLanguageVersion.V36, "p: 1=optimized | 2=newlocals | 4=*arg | 8=**arg");
        }

        [TestMethod, Priority(1)]
        public void TestExplicitLineJoin() {
            TestOneString(PythonLanguageVersion.V27, @"fob(4 + \
                    5)");
        }

        [TestMethod, Priority(1)]
        public void TestTrailingComment() {
            TestOneString(PythonLanguageVersion.V27, $"def f(): pass{Environment.NewLine}#fob");
        }

        [TestMethod, Priority(1)]
        public void TestStatements() {
            // TODO: Vary all of these tests by putting the test case in a function def
            // TODO: Vary all of these tests by adding trailing comments                        
            TestOneString(PythonLanguageVersion.V27, "def _process_result(self, (i");

            // Empty Statement
            TestOneString(PythonLanguageVersion.V27, "pass");
            
            // Break Statement
            TestOneString(PythonLanguageVersion.V27, "break");
            
            // Continue Statement
            TestOneString(PythonLanguageVersion.V27, "continue");

            // Non local statement
            TestOneString(PythonLanguageVersion.V30, "nonlocal abc");
            TestOneString(PythonLanguageVersion.V30, "nonlocal abc, baz");
            TestOneString(PythonLanguageVersion.V30, "nonlocal  abc   ,    baz");

            // Global Statement
            TestOneString(PythonLanguageVersion.V27, "global abc");
            TestOneString(PythonLanguageVersion.V27, "global abc, baz");
            TestOneString(PythonLanguageVersion.V27, "global  abc   ,    baz");

            // Return Statement
            TestOneString(PythonLanguageVersion.V27, "return");
            TestOneString(PythonLanguageVersion.V27, "return 42");
            TestOneString(PythonLanguageVersion.V27, "return 42,");
            TestOneString(PythonLanguageVersion.V27, "return 42,43");
            TestOneString(PythonLanguageVersion.V27, "return  42   ,    43");

            // Del Statement
            TestOneString(PythonLanguageVersion.V27, "del");
            TestOneString(PythonLanguageVersion.V27, "del abc");
            TestOneString(PythonLanguageVersion.V27, "del abc,");
            TestOneString(PythonLanguageVersion.V27, "del abc,baz");
            TestOneString(PythonLanguageVersion.V27, "del  abc   ,    baz     ,");

            // Raise Statement
            TestOneString(PythonLanguageVersion.V27, "raise");
            TestOneString(PythonLanguageVersion.V27, "raise fob");
            TestOneString(PythonLanguageVersion.V27, "raise fob, oar");
            TestOneString(PythonLanguageVersion.V27, "raise fob, oar, baz");
            TestOneString(PythonLanguageVersion.V30, "raise fob from oar");
            TestOneString(PythonLanguageVersion.V27, "raise  fob");
            TestOneString(PythonLanguageVersion.V27, "raise  fob   ,    oar");
            TestOneString(PythonLanguageVersion.V27, "raise  fob   ,    oar     ,      baz");
            TestOneString(PythonLanguageVersion.V30, "raise  fob   from    oar");

            // Assert Statement
            TestOneString(PythonLanguageVersion.V27, "assert fob");
            TestOneString(PythonLanguageVersion.V27, "assert fob, oar");
            TestOneString(PythonLanguageVersion.V27, "assert  fob");
            TestOneString(PythonLanguageVersion.V27, "assert  fob   ,    oar");

            // Import Statement
            TestOneString(PythonLanguageVersion.V27, "import sys");
            TestOneString(PythonLanguageVersion.V27, "import sys as fob");
            TestOneString(PythonLanguageVersion.V27, "import sys as fob, itertools");
            TestOneString(PythonLanguageVersion.V27, "import sys as fob, itertools as i");
            TestOneString(PythonLanguageVersion.V27, "import  sys");
            TestOneString(PythonLanguageVersion.V27, "import  sys   as    fob");
            TestOneString(PythonLanguageVersion.V27, "import  sys   as    fob     ,       itertools");
            TestOneString(PythonLanguageVersion.V27, "import  sys   as    fob     ,       itertools       as        i");
            TestOneString(PythonLanguageVersion.V27, "import X, Y, Z, A as B");

            // From Import Statement
            TestOneString(PythonLanguageVersion.V27, "from sys import *");
            TestOneString(PythonLanguageVersion.V27, "from sys import platform");
            TestOneString(PythonLanguageVersion.V27, "from sys import platform as pt");
            TestOneString(PythonLanguageVersion.V27, "from sys import platform as pt, stdin as si");
            TestOneString(PythonLanguageVersion.V27, "from sys import (platform)");
            TestOneString(PythonLanguageVersion.V27, "from sys import (platform,)");
            TestOneString(PythonLanguageVersion.V27, "from  sys   import    *");
            TestOneString(PythonLanguageVersion.V27, "from  sys   import    platform");
            TestOneString(PythonLanguageVersion.V27, "from  sys   import    platform     as      pt");
            TestOneString(PythonLanguageVersion.V27, "from  sys   import    platform     as      pt       ,        stdin         as           si");
            TestOneString(PythonLanguageVersion.V27, "from  sys   import    (     platform      )");
            TestOneString(PythonLanguageVersion.V27, "from  sys   import    (     platform       as       pt        )");
            TestOneString(PythonLanguageVersion.V27, "from  sys   import    (     platform       as       pt        ,         stdin          as          si           )");
            TestOneString(PythonLanguageVersion.V27, "from  sys   import    (     platform       ,        )");
            TestOneString(PythonLanguageVersion.V27, "from xyz import A, B, C, D, E");


            // Assignment statement
            TestOneString(PythonLanguageVersion.V27, "x = 42");
            TestOneString(PythonLanguageVersion.V27, "x  =   42");
            TestOneString(PythonLanguageVersion.V27, "x = abc = 42");
            TestOneString(PythonLanguageVersion.V27, "x  =   abc    =     42");
            TestOneString(PythonLanguageVersion.V30, $"def f():{Environment.NewLine}     a = True");

            // Augmented Assignment Statement
            foreach (var op in new[] { "+", "-", "*", "/", "//", "%", "&", "|", "^", "<<", ">>", "**"}) {
                TestOneString(PythonLanguageVersion.V27, "x " + op + "= 42");
                TestOneString(PythonLanguageVersion.V27, "x  " + op + "   42");
            }

            // Exec Statement
            TestOneString(PythonLanguageVersion.V27, "exec 'abc'");
            TestOneString(PythonLanguageVersion.V27, "exec 'abc' in l");
            TestOneString(PythonLanguageVersion.V27, "exec 'abc' in l, g");
            TestOneString(PythonLanguageVersion.V27, "exec  'abc'");
            TestOneString(PythonLanguageVersion.V27, "exec  'abc'   in    l");
            TestOneString(PythonLanguageVersion.V27, "exec  'abc'   in    l     ,      g");
            TestOneString(PythonLanguageVersion.V27, "exec(a, b, c)");
            TestOneString(PythonLanguageVersion.V27, "exec  ( a, b, c )");

            // Print Statement
            TestOneString(PythonLanguageVersion.V27, "print fob");
            TestOneString(PythonLanguageVersion.V27, "print fob, oar");
            TestOneString(PythonLanguageVersion.V27, "print fob,");
            TestOneString(PythonLanguageVersion.V27, "print fob, oar,"); 
            TestOneString(PythonLanguageVersion.V27, "print >> dest");
            TestOneString(PythonLanguageVersion.V27, "print >> dest, fob");
            TestOneString(PythonLanguageVersion.V27, "print >> dest, fob, oar");
            TestOneString(PythonLanguageVersion.V27, "print >> dest, fob,");
            TestOneString(PythonLanguageVersion.V27, "print >> dest, fob, oar,");
            TestOneString(PythonLanguageVersion.V27, "print  fob");
            TestOneString(PythonLanguageVersion.V27, "print  fob   ,    oar");
            TestOneString(PythonLanguageVersion.V27, "print  fob   ,");
            TestOneString(PythonLanguageVersion.V27, "print  fob   ,    oar     ,");
            TestOneString(PythonLanguageVersion.V27, "print  >>   dest");
            TestOneString(PythonLanguageVersion.V27, "print  >>   dest    ,     fob");
            TestOneString(PythonLanguageVersion.V27, "print  >>   dest    ,     fob      ,       oar");
            TestOneString(PythonLanguageVersion.V27, "print  >>   dest    ,     fob      ,");
            TestOneString(PythonLanguageVersion.V27, "print  >>   dest    ,     fob      ,       oar        ,");
            TestOneString(PythonLanguageVersion.V27, "print l1==l");


            // For Statement
            TestOneString(PythonLanguageVersion.V27, "for i in xrange(10): pass");
            TestOneString(PythonLanguageVersion.V27, $"for i in xrange(10):{Environment.NewLine}    pass{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"for i in xrange(10):{Environment.NewLine}{Environment.NewLine}    pass{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"for i in xrange(10):{Environment.NewLine}    break{Environment.NewLine}else:{Environment.NewLine}    pass");
            
            TestOneString(PythonLanguageVersion.V27, "for (i), (j) in x.items(): print(i, j)");
            TestOneString(PythonLanguageVersion.V27, "for (i, j) in x.items(): print(i, j)");
            TestOneString(PythonLanguageVersion.V27, "for i,j in xrange(10): pass");
            TestOneString(PythonLanguageVersion.V27, "for ((i, j)) in x.items(): print(i, j)");
            TestOneString(PythonLanguageVersion.V27, "for (((i), (j))) in x.items(): print(i, j)");
            TestOneString(PythonLanguageVersion.V27, "for [i, j] in x.items(): print(i, j)");
            TestOneString(PythonLanguageVersion.V27, "for [[i], [j]] in x.items(): print(i, j)");

            TestOneString(PythonLanguageVersion.V27, "for  i   in    xrange(10)    :      pass");
            TestOneString(PythonLanguageVersion.V27, $"for  i   in    xrange(10)    :{Environment.NewLine}    pass {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"for  i   in    xrange(10)    :{Environment.NewLine}{Environment.NewLine}    pass {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"for  i   in    xrange(10)    :{Environment.NewLine}    break{Environment.NewLine}else     :      {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, "for  (i), (j)   in    x.items()     :      print(i, j)");
            TestOneString(PythonLanguageVersion.V27, "for  (i, j)   in    x.items()     :      print(i, j)");
            TestOneString(PythonLanguageVersion.V27, "for  i,j    in    xrange(10)     :      pass");
            TestOneString(PythonLanguageVersion.V27, "for  ((i, j))   in    x.items()     :       print(i, j)");
            TestOneString(PythonLanguageVersion.V27, "for  (((i), (j)))   in    x.items()     :      print(i, j)");
            TestOneString(PythonLanguageVersion.V27, "for  [i, j]   in    x.items()     :      print(i, j)");
            TestOneString(PythonLanguageVersion.V27, "for  [[i], [j]]   in    x.items()     :      print(i, j)");

            TestOneString(PythonLanguageVersion.V35, $"async def f():{Environment.NewLine}    async for i in xrange(10): pass");
            TestOneString(PythonLanguageVersion.V35, $"async def f():{Environment.NewLine}    async  for i in xrange(10):{Environment.NewLine}        pass{Environment.NewLine}        pass");
            TestOneString(PythonLanguageVersion.V35, $"async def f():{Environment.NewLine}    async  for  i in xrange(10):{Environment.NewLine}{Environment.NewLine}        pass{Environment.NewLine}        pass");
            TestOneString(PythonLanguageVersion.V35, $"async def f():{Environment.NewLine}    async for  i  in xrange(10):{Environment.NewLine}        break{Environment.NewLine}else:{Environment.NewLine}        pass");

            // While Statement
            TestOneString(PythonLanguageVersion.V27, "while True: break");
            TestOneString(PythonLanguageVersion.V27, $"while True: break{Environment.NewLine}else: pass");
            TestOneString(PythonLanguageVersion.V27, $"while True:{Environment.NewLine}    break{Environment.NewLine}else:{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, "while  True   :    break");
            TestOneString(PythonLanguageVersion.V27, $"while  True   :    break{Environment.NewLine}else     : pass");
            TestOneString(PythonLanguageVersion.V27, $"while  True:{Environment.NewLine}    break   {Environment.NewLine}else    :     {Environment.NewLine}    pass");

            // If Statement
            TestOneString(PythonLanguageVersion.V27, "if True: pass");
            TestOneString(PythonLanguageVersion.V27, $"if True:{Environment.NewLine}    pass{Environment.NewLine}else:{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"if True:{Environment.NewLine}    pass{Environment.NewLine}elif False:{Environment.NewLine}    pass{Environment.NewLine}else:{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, "if  True   :    pass");
            TestOneString(PythonLanguageVersion.V27, $"if  True   :{Environment.NewLine}    pass{Environment.NewLine}else    :     {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"if  True   :{Environment.NewLine}    pass{Environment.NewLine}elif     False     :{Environment.NewLine}    pass      {Environment.NewLine}else       :        {Environment.NewLine}    pass");

            // Suite Statement
            TestOneString(PythonLanguageVersion.V27, "abc;fob;oar");
            TestOneString(PythonLanguageVersion.V27, "abc  ;   fob    ;     oar");
            TestOneString(PythonLanguageVersion.V27, $"abc;fob{Environment.NewLine}{Environment.NewLine}oar;baz");
            TestOneString(PythonLanguageVersion.V27, $"abc  ;   fob    {Environment.NewLine}{Environment.NewLine}oar     ;      baz");
            TestOneString(PythonLanguageVersion.V27, "fob;");
            TestOneString(PythonLanguageVersion.V27, $"def f():{Environment.NewLine}    if True:{Environment.NewLine}        fob;{Environment.NewLine}     oar");
            TestOneString(PythonLanguageVersion.V27, @"def f(x):
    length = x
    if length == 0:
        pass
");
            TestOneString(PythonLanguageVersion.V27, @"def f():
    try:
        return 42
    except Exception:
        pass");

            // With Statement
            TestOneString(PythonLanguageVersion.V27, "with abc: pass");
            TestOneString(PythonLanguageVersion.V27, "with abc as oar: pass");
            TestOneString(PythonLanguageVersion.V27, "with fob, oar: pass");
            TestOneString(PythonLanguageVersion.V27, "with fob as f, oar as b: pass");
            TestOneString(PythonLanguageVersion.V27, "with  abc   : pass");
            TestOneString(PythonLanguageVersion.V27, "with  abc   as    oar     :      pass");
            TestOneString(PythonLanguageVersion.V27, "with  fob   ,    oar     :      pass");
            TestOneString(PythonLanguageVersion.V27, "with  fob   as    f     ,       oar       as       b        :          pass");
            TestOneString(PythonLanguageVersion.V27, "with abc: pass");
            TestOneString(PythonLanguageVersion.V27, $"with abc as oar:{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"with fob, oar:{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"with fob as f, oar as b:{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"with  abc   :{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"with  abc   as    oar     :  {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"with  fob   ,    oar     :  {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"with  fob   as    f     ,       oar       as       b        :  {Environment.NewLine}    pass");

            TestOneString(PythonLanguageVersion.V35, $"async def f():{Environment.NewLine}    async with abc: pass");
            TestOneString(PythonLanguageVersion.V35, $"async def f():{Environment.NewLine}    async  with  abc   :{Environment.NewLine}        pass");
            TestOneString(PythonLanguageVersion.V35, $"async def f():{Environment.NewLine}    async   with  fob   ,    oar     :{Environment.NewLine}          pass");
            TestOneString(PythonLanguageVersion.V35, $"async def f():{Environment.NewLine}    async  with  fob   ,    oar     :  {Environment.NewLine}        pass");

            // Try Statement
            TestOneString(PythonLanguageVersion.V27, $"try: pass{Environment.NewLine}except: pass");
            TestOneString(PythonLanguageVersion.V27, $"try: pass{Environment.NewLine}except Exception: pass");
            TestOneString(PythonLanguageVersion.V27, $"try: pass{Environment.NewLine}except Exception, e: pass");
            TestOneString(PythonLanguageVersion.V27, $"try: pass{Environment.NewLine}except Exception as e: pass");
            TestOneString(PythonLanguageVersion.V27, $"try: pass{Environment.NewLine}except TypeError: pass{Environment.NewLine}except Exception: pass");
            TestOneString(PythonLanguageVersion.V27, $"try: pass{Environment.NewLine}except TypeError, e: pass{Environment.NewLine}except Exception: pass");
            TestOneString(PythonLanguageVersion.V27, $"try: pass{Environment.NewLine}except TypeError as e: pass{Environment.NewLine}except Exception: pass");
            TestOneString(PythonLanguageVersion.V27, $"try: pass{Environment.NewLine}except: pass{Environment.NewLine}else: pass");
            TestOneString(PythonLanguageVersion.V27, $"try: pass{Environment.NewLine}except Exception: pass{Environment.NewLine}else: pass");
            TestOneString(PythonLanguageVersion.V27, $"try: pass{Environment.NewLine}except Exception, e: pass{Environment.NewLine}else: pass");
            TestOneString(PythonLanguageVersion.V27, $"try: pass{Environment.NewLine}except Exception as e: pass{Environment.NewLine}else: pass");
            TestOneString(PythonLanguageVersion.V27, $"try: pass{Environment.NewLine}except: pass{Environment.NewLine}finally: pass");
            TestOneString(PythonLanguageVersion.V27, $"try: pass{Environment.NewLine}except Exception: pass{Environment.NewLine}finally: pass");
            TestOneString(PythonLanguageVersion.V27, $"try: pass{Environment.NewLine}except Exception, e: pass{Environment.NewLine}finally: pass");
            TestOneString(PythonLanguageVersion.V27, $"try: pass{Environment.NewLine}except Exception as e: pass{Environment.NewLine}finally: pass");
            TestOneString(PythonLanguageVersion.V27, $"try: pass{Environment.NewLine}except: pass{Environment.NewLine}else: pass{Environment.NewLine}finally: pass");
            TestOneString(PythonLanguageVersion.V27, $"try: pass{Environment.NewLine}except Exception: pass{Environment.NewLine}else: pass{Environment.NewLine}finally: pass");
            TestOneString(PythonLanguageVersion.V27, $"try: pass{Environment.NewLine}except Exception, e: pass{Environment.NewLine}else: pass{Environment.NewLine}finally: pass");
            TestOneString(PythonLanguageVersion.V27, $"try: pass{Environment.NewLine}except Exception as e: pass{Environment.NewLine}else: pass{Environment.NewLine}finally: pass");

            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass{Environment.NewLine}except:{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass{Environment.NewLine}except Exception:{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass{Environment.NewLine}except Exception, e:{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass{Environment.NewLine}except Exception as e:{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass{Environment.NewLine}except TypeError:{Environment.NewLine}    pass{Environment.NewLine}except Exception:{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass{Environment.NewLine}except TypeError, e:{Environment.NewLine}    pass{Environment.NewLine}except Exception:{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass{Environment.NewLine}except TypeError as e:{Environment.NewLine}    pass{Environment.NewLine}except Exception:{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass{Environment.NewLine}except:{Environment.NewLine}    pass{Environment.NewLine}else:{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass{Environment.NewLine}except Exception:{Environment.NewLine}    pass{Environment.NewLine}else:{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass{Environment.NewLine}except Exception, e:{Environment.NewLine}    pass{Environment.NewLine}else:{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass{Environment.NewLine}except Exception as e:{Environment.NewLine}    pass{Environment.NewLine}else:{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass{Environment.NewLine}except: pass{Environment.NewLine}finally:{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass{Environment.NewLine}except Exception:{Environment.NewLine}    pass{Environment.NewLine}finally:{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass{Environment.NewLine}except Exception, e:{Environment.NewLine}    pass{Environment.NewLine}finally:{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass{Environment.NewLine}except Exception as e:{Environment.NewLine}    pass{Environment.NewLine}finally:{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass{Environment.NewLine}except: pass{Environment.NewLine}else:{Environment.NewLine}    pass{Environment.NewLine}finally:{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass{Environment.NewLine}except Exception:{Environment.NewLine}    pass{Environment.NewLine}else: pass{Environment.NewLine}finally:{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass{Environment.NewLine}except Exception, e:{Environment.NewLine}    pass{Environment.NewLine}else:{Environment.NewLine}    pass{Environment.NewLine}finally:{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass{Environment.NewLine}except Exception as e:{Environment.NewLine}    pass{Environment.NewLine}else:{Environment.NewLine}    pass{Environment.NewLine}finally:{Environment.NewLine}    pass");

            TestOneString(PythonLanguageVersion.V27, $"try:  pass{Environment.NewLine}except   :    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:  pass{Environment.NewLine}except   Exception    :     pass");
            TestOneString(PythonLanguageVersion.V27, $"try:  pass{Environment.NewLine}except   Exception    ,     e      :        pass");
            TestOneString(PythonLanguageVersion.V27, $"try:  pass{Environment.NewLine}except   Exception    as    e      :        pass");
            TestOneString(PythonLanguageVersion.V27, $"try:  pass{Environment.NewLine}except   TypeError    :     pass      {Environment.NewLine}except        Exception        :          pass");
            TestOneString(PythonLanguageVersion.V27, $"try:  pass{Environment.NewLine}except   TypeError    ,     e        :          pass{Environment.NewLine}except           Exception            :            pass");
            TestOneString(PythonLanguageVersion.V27, $"try:  pass{Environment.NewLine}except   TypeError    as    e        :          pass{Environment.NewLine}except           Exception             :              pass");
            TestOneString(PythonLanguageVersion.V27, $"try:  pass{Environment.NewLine}except   :    pass     {Environment.NewLine}else      :         pass");
            TestOneString(PythonLanguageVersion.V27, $"try:  pass{Environment.NewLine}except   Exception    :      pass     {Environment.NewLine}else       :        pass");
            TestOneString(PythonLanguageVersion.V27, $"try:  pass{Environment.NewLine}except   Exception    ,     e      :       pass        {Environment.NewLine}else         :              pass");
            TestOneString(PythonLanguageVersion.V27, $"try:  pass{Environment.NewLine}except   Exception    as     e      :       pass        {Environment.NewLine}else         :              pass");
            TestOneString(PythonLanguageVersion.V27, $"try:  pass{Environment.NewLine}except   :    pass     {Environment.NewLine}finally      :       pass");
            TestOneString(PythonLanguageVersion.V27, $"try:  pass{Environment.NewLine}except   Exception    :      pass       {Environment.NewLine}finally       :        pass");
            TestOneString(PythonLanguageVersion.V27, $"try:  pass{Environment.NewLine}except   Exception    ,      e       :       pass{Environment.NewLine}finally         :          pass");
            TestOneString(PythonLanguageVersion.V27, $"try:  pass{Environment.NewLine}except   Exception    as      e       :       pass{Environment.NewLine}finally         :          pass");
            TestOneString(PythonLanguageVersion.V27, $"try:  pass{Environment.NewLine}except   :    pass{Environment.NewLine}else     :      pass       {Environment.NewLine}finally        :        pass");
            TestOneString(PythonLanguageVersion.V27, $"try:  pass{Environment.NewLine}except   Exception    :     pass      {Environment.NewLine}else       :        pass         {Environment.NewLine}finally          :           pass");
            TestOneString(PythonLanguageVersion.V27, $"try:  pass{Environment.NewLine}except   Exception    ,    e:     pass      {Environment.NewLine}else       :       pass         {Environment.NewLine}finally          :          pass");
            TestOneString(PythonLanguageVersion.V27, $"try:  pass{Environment.NewLine}except   Exception    as    e:     pass      {Environment.NewLine}else       :       pass         {Environment.NewLine}finally          :          pass");

            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass     {Environment.NewLine}except      :{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass     {Environment.NewLine}except       Exception       :{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass     {Environment.NewLine}except       Exception       ,        e         :          {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass     {Environment.NewLine}except       Exception       as        e          :{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass     {Environment.NewLine}except       TypeError       :        {Environment.NewLine}    pass{Environment.NewLine}except Exception:{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass     {Environment.NewLine}except       TypeError       ,        e         :{Environment.NewLine}    pass{Environment.NewLine}except Exception:{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass     {Environment.NewLine}except       TypeError       as        e         :{Environment.NewLine}    pass{Environment.NewLine}except Exception:{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass     {Environment.NewLine}except      :{Environment.NewLine}    pass    {Environment.NewLine}else        :{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass     {Environment.NewLine}except       Exception       :        {Environment.NewLine}    pass{Environment.NewLine}else        :         {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass     {Environment.NewLine}except       Exception       ,         e         :{Environment.NewLine}    pass{Environment.NewLine}else          :{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass     {Environment.NewLine}except       Exception       as       e        :{Environment.NewLine}    pass{Environment.NewLine}else          :          {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass     {Environment.NewLine}except      : pass      {Environment.NewLine}finally       :         {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass     {Environment.NewLine}except       Exception      :       {Environment.NewLine}    pass{Environment.NewLine}finally          :{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass     {Environment.NewLine}except       Exception       ,        e          :{Environment.NewLine}    pass{Environment.NewLine}finally:{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass     {Environment.NewLine}except       Exception       as        e         :{Environment.NewLine}    pass{Environment.NewLine}finally           :            {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass     {Environment.NewLine}except      :        pass        {Environment.NewLine}else          :          {Environment.NewLine}    pass{Environment.NewLine}finally           :           {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass     {Environment.NewLine}except       Exception       :{Environment.NewLine}    pass{Environment.NewLine}else        :         pass{Environment.NewLine}finally          :           {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass     {Environment.NewLine}except       Exception       ,       e         :{Environment.NewLine}    pass{Environment.NewLine}else           :             {Environment.NewLine}    pass{Environment.NewLine}finally             :               {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass     {Environment.NewLine}except       Exception       as       e        :{Environment.NewLine}    pass{Environment.NewLine}else           :{Environment.NewLine}    pass{Environment.NewLine}finally          :              {Environment.NewLine}    pass");

            TestOneString(PythonLanguageVersion.V27, $"try: pass{Environment.NewLine}finally: pass");
            TestOneString(PythonLanguageVersion.V27, $"try  :   pass{Environment.NewLine}finally    :     pass");
            TestOneString(PythonLanguageVersion.V27, $"try:{Environment.NewLine}    pass{Environment.NewLine}finally:{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, $"try  :   {Environment.NewLine}    pass{Environment.NewLine}finally    :     {Environment.NewLine}    pass");

            // Class Definition
            TestOneString(PythonLanguageVersion.V27, "class C: pass");
            TestOneString(PythonLanguageVersion.V27, "class C(): pass");
            TestOneString(PythonLanguageVersion.V27, "class C(object): pass");
            TestOneString(PythonLanguageVersion.V27, "class C(object, ): pass");
            TestOneString(PythonLanguageVersion.V30, "class C(object, metaclass=42): pass");
            TestOneString(PythonLanguageVersion.V30, "class C(*fob): pass");
            TestOneString(PythonLanguageVersion.V30, "class C(*fob, ): pass");
            TestOneString(PythonLanguageVersion.V30, "class C(*fob, **oar): pass");
            TestOneString(PythonLanguageVersion.V30, "class C(*fob, **oar, ): pass");
            TestOneString(PythonLanguageVersion.V30, "class C(**fob): pass");
            TestOneString(PythonLanguageVersion.V30, "class C(**fob, ): pass"); 
            TestOneString(PythonLanguageVersion.V30, "class C(fob = oar): pass");
            TestOneString(PythonLanguageVersion.V30, "class C(fob = oar, baz = 42): pass");

            TestOneString(PythonLanguageVersion.V27, "class  C   :    pass");
            TestOneString(PythonLanguageVersion.V27, "class  C   (    )     :      pass");
            TestOneString(PythonLanguageVersion.V27, "class  C   (    object): pass");
            TestOneString(PythonLanguageVersion.V27, "class  C   (    object      ,       )      : pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    object      ,       metaclass        =         42          )           : pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    *     fob      )       :         pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    *     fob      ,       )        :        pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    *     fob      ,       **        oar         )          :           pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    *     fob      ,      **        oar         ,          )           :            pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    **     fob      )       :         pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    **     fob      ,       )        :         pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    fob     =      oar       )        :         pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    fob     =      oar       ,        baz         =          42           )           :             pass");

            TestOneString(PythonLanguageVersion.V27, "class C: {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, "class C(): {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, "class C(object): {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, "class C(object, ): {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V30, "class C(object, metaclass=42): {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V30, "class C(*fob): {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V30, "class C(*fob, ): {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V30, "class C(*fob, **oar): {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V30, "class C(*fob, **oar, ): {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V30, "class C(**fob): {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V30, "class C(**fob, ): {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V30, "class C(fob = oar): {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V30, "class C(fob = oar, baz = 42): {Environment.NewLine}    pass");

            TestOneString(PythonLanguageVersion.V27, "class  C   :    {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, "class  C   (    )     :      {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, "class  C   (    object): {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, "class  C   (    object      ,       )      : {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    object      ,       metaclass        =         42          )           : {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    *     fob      )       :         {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    *     fob      ,       )        :        {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    *     fob      ,       **        oar         )          :           {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    *     fob      ,      **        oar         ,          )           :            {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    **     fob      )       :         {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    **     fob      ,       )        :         {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    fob     =      oar       )        :         {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    fob     =      oar       ,        baz         =          42           )           :             {Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, "class Fob(int if y else object):{Environment.NewLine}    pass");
            TestOneString(PythonLanguageVersion.V27, "class  Fob   (    int     if      y      else       object         )         :{Environment.NewLine}    pass");

            TestOneString(PythonLanguageVersion.V27, $"@fob{Environment.NewLine}class C: pass");
            TestOneString(PythonLanguageVersion.V27, $"@  fob   {Environment.NewLine}class    C     :       pass");

            // Function Definition
            TestOneString(PythonLanguageVersion.V27, "def f(): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(a): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(a = 42): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(a, b): pass");
            TestOneString(PythonLanguageVersion.V30, "def f(a, b) -> fob: pass");
            TestOneString(PythonLanguageVersion.V27, "def f(*a, **b): pass");
            TestOneString(PythonLanguageVersion.V27, "def  f   (    )     :       pass");
            TestOneString(PythonLanguageVersion.V27, "def  f   (    a     )      :        pass");
            TestOneString(PythonLanguageVersion.V27, "def  f   (    a     =       42        )          :           pass");
            TestOneString(PythonLanguageVersion.V27, "def  f   (    a     ,       b          )          :           pass");
            TestOneString(PythonLanguageVersion.V30, "def  f   (    a     ,       b        )         ->          fob           :            pass");
            TestOneString(PythonLanguageVersion.V27, "def  f   (    *     a      ,        **        b         )          :           pass");
            TestOneString(PythonLanguageVersion.V27, $"@fob{Environment.NewLine}def f(): pass");
            TestOneString(PythonLanguageVersion.V27, $"@fob.oar{Environment.NewLine}def f(): pass");
            TestOneString(PythonLanguageVersion.V27, $"@fob(2){Environment.NewLine}def f(): pass");
            TestOneString(PythonLanguageVersion.V27, $"@fob.oar(2){Environment.NewLine}def f(): pass");
            TestOneString(PythonLanguageVersion.V27, $"@  fob   {Environment.NewLine}def f(): pass");
            TestOneString(PythonLanguageVersion.V27, $"@  fob   .    oar{Environment.NewLine}def f(): pass");
            TestOneString(PythonLanguageVersion.V27, $"@  fob   (    2     ){Environment.NewLine}def f(): pass");
            TestOneString(PythonLanguageVersion.V27, $"@  fob   .    oar     (      2       ){Environment.NewLine}def f(): pass");
            TestOneString(PythonLanguageVersion.V27, "def f((a)): pass");
            TestOneString(PythonLanguageVersion.V27, "def  f   (    (     a         )      )       :        pass");
            TestOneString(PythonLanguageVersion.V27, "def f((a, b)): pass");
            TestOneString(PythonLanguageVersion.V27, "def  f   (    (     a         ,      b)       )         :           pass");
            TestOneString(PythonLanguageVersion.V27, "def f((a, (b, c))): pass");
            TestOneString(PythonLanguageVersion.V27, "def  f   (    (     a      ,       (         b          ,          c            )             )              )              :                pass");

            TestOneString(PythonLanguageVersion.V27, $"@fob{Environment.NewLine}{Environment.NewLine}def f(): pass");

            TestOneString(PythonLanguageVersion.V27, $"class C:{Environment.NewLine}    @fob.__oar{Environment.NewLine}    def f(self): pass");

            TestOneString(PythonLanguageVersion.V27, $"class C:{Environment.NewLine}    def __f(self): pass");

            TestOneString(PythonLanguageVersion.V27, "def f(a,): pass");
            TestOneString(PythonLanguageVersion.V27, "def  f(   a    ,     )      :       pass");

            TestOneString(PythonLanguageVersion.V27, $"class C:{Environment.NewLine}    @property{Environment.NewLine}    def fob(self): return 42");

            TestOneString(PythonLanguageVersion.V35, "async def f(): pass");
            TestOneString(PythonLanguageVersion.V35, $"@fob{Environment.NewLine}{Environment.NewLine}async def f(): pass");
            TestOneString(PythonLanguageVersion.V35, $"@fob(2){Environment.NewLine}async \\{Environment.NewLine}def f(): pass");

            TestOneString(PythonLanguageVersion.V35, "def f(a, ");

            // Variable annotations
            TestOneString(PythonLanguageVersion.V36, "a:b");
            TestOneString(PythonLanguageVersion.V36, "a:b = 1");
            TestOneString(PythonLanguageVersion.V36, "a : b");
            TestOneString(PythonLanguageVersion.V36, "a : b = 1");

        }

        [TestMethod, Priority(0)]
        public void RoundTripSublistParameterWithDefault() {
            TestOneString(PythonLanguageVersion.V27, $"def f((a, b) = (1, 2)):{Environment.NewLine}    pass");
        }

        [TestMethod, Priority(0)]
        public void RoundTripDoubleAwait() {
            TestOneString(PythonLanguageVersion.V35, $"async def f(x):{Environment.NewLine}    await await x");
        }

        [TestMethod, Priority(0)]
        public void RoundTripExecTupleIn() {
            TestOneString(PythonLanguageVersion.V27, "exec(f, g, h) in i, j");
            TestOneString(PythonLanguageVersion.V27, "exec f in g");
            TestOneString(PythonLanguageVersion.V27, "exec(f,g)");
        }

        [TestMethod, Priority(0)]
        public void RoundTripErrorParameter() {
            TestOneString(PythonLanguageVersion.V35, "def inner(_it, _timer{init}): pass");
        }

        [TestMethod, Priority(0)]
        public void RoundTripInvalidClassName() {
            TestOneString(PythonLanguageVersion.V35, "\nclass {{ invalid_name }}MyClass:\n");
        }


        private static void RoundTripStdLibTest(InterpreterConfiguration configuration) {
            configuration.AssertInstalled();

            Console.WriteLine("Testing version {0} {1}", configuration.Version, configuration.InterpreterPath);

            int ran = 0, succeeded = 0;
            var pathResolver = new PathResolver(configuration.Version.ToLanguageVersion(),
                string.Empty,
                ImmutableArray<string>.Create(configuration.SitePackagesPath, configuration.LibraryPath),
                ImmutableArray<string>.Empty);
            var pathResolverSnapshot = pathResolver.CurrentSnapshot;

            var modules = pathResolverSnapshot.GetAllImportableModuleNames()
                .Select(n => pathResolverSnapshot.GetModuleImportFromModuleName(n))
                .Where(i => i.RootPath.PathEquals(configuration.SitePackagesPath))
                .ToList();

            foreach (var module in modules) {
                try {
                    if (!module.IsCompiled) {
                        ran++;
                        TestOneFile(module.ModulePath, configuration.Version.ToLanguageVersion());
                        succeeded++;
                    }
                } catch (Exception e) {
                    Console.WriteLine(e);
                    Console.WriteLine("Failed: {0}", module);
                    break;
                }
            }

            Assert.AreEqual(ran, succeeded);
        }

        [TestMethod, Priority(0)]
        public void RoundTripStdLib27() => RoundTripStdLibTest(PythonVersions.Python27 ?? PythonVersions.Python27_x64);

        [TestMethod, Priority(0)]
        public void RoundTripStdLib35() => RoundTripStdLibTest(PythonVersions.Python35 ?? PythonVersions.Python35_x64);

        [TestMethod, Priority(0)]
        public void RoundTripStdLib36() => RoundTripStdLibTest(PythonVersions.Python36 ?? PythonVersions.Python36_x64);

        [TestMethod, Priority(0)]
        public void RoundTripStdLib37() => RoundTripStdLibTest(PythonVersions.Python37 ?? PythonVersions.Python37_x64);

        [TestMethod, Priority(0)]
        public void GroupingRecovery() {
            // The exact text below hit an issue w/ grouping recovery where the buffer wrapped
            // and our grouping recovery was invalid, but we thought it was valid due to the
            // wrapping.  Adding or removing a single byte from the text below will invalidate
            // the test case.
            TestOneString(PythonLanguageVersion.V27,
                @"ts (including sets of sets).

This module implements sets using dictionaries whose values are
ignored.  The usual operations (union, intersection, deletion, etc.)
are provided as both methods and operators.

Important: sets are not sequences!  While they support 'x in s',
'len(s)', and 'for x in s', none of those operations are unique for
sequences; for example, mappings support all three as well.  The
characteristic operation for sequences is subscripting with small
integers: s[i], for i in range(len(s)).  Sets don't support
subscripting at all.  Also, sequences allow multiple occurrences and
their elements have a definite order; sets on the other hand don't
record multiple occurrences and don't remember the order of element
insertion (which is why they don't support s[i]).

The following classes are provided:

BaseSet -- All the operations common to both mutable and immutable
    sets. This is an abstract class, not meant to be directly
    instantiated.

Set -- Mutable sets, subclass of BaseSet; not hashable.

ImmutableSet -- Immutable sets, subclass of BaseSet; hashable.
    An iterable argument is mandatory to create an ImmutableSet.

_TemporarilyImmutableSet -- A wrapper around a Set, hashable,
    giving the same hash value as the immutable set equivalent
    would have.  Do not use this class directly.

Only hashable objects can be added to a Set. In particular, you cannot
really add a Set as an element to another Set; if you try, what is
actually added is an ImmutableSet built from it (it compares equal to
the one you tried adding).

When you ask if `x in y' where x is a Set and y is a Set or
ImmutableSet, x is wrapped into a _TemporarilyImmutableSet z, and
what's tested is actually `z in y'.

""""""

# Code history:
#
# - Greg V. Wilson wrote the first version, using a different approach
#   to the mutable/immutable problem, and inheriting from dict.
#
# - Alex Martelli modified Greg's version to implement the current
#   Set/ImmutableSet approach, and make the data an attribute.
#
# - Guido van Rossum rewrote much of the code, made some API changes,
#   and cleaned up the docstrings.
#
# - Raymond Hettinger added a number of speedups and other
#   improvements.

from itertools import ifilter, ifilterfalse

__all__ = ['BaseSet', 'Set', 'ImmutableSet']

import warnings
warnings.warn(""the sets module is deprecated"", DeprecationWarning,
                stacklevel=2)

class BaseSet(object):
    """"""Common base class for mutable and immutable sets.");
        }

        [TestMethod, Priority(0)]
        public void GeneralizedUnpacking() {
            TestOneString(PythonLanguageVersion.V35, "list_ = [  *a, *b, c,*d]");
            TestOneString(PythonLanguageVersion.V35, "tuple_ =   *a, *b, c,*d");
            TestOneString(PythonLanguageVersion.V35, "paren_tuple = (  *a, *b, c,*d)");
            TestOneString(PythonLanguageVersion.V35, "set_ = {  *a, *b, c,*d}");
            TestOneString(PythonLanguageVersion.V35, "dict_ = {  **a, **b, c: 'c',**d}");
        }

        private static void TestOneFile(string filename, PythonLanguageVersion version) {
            var originalText = File.ReadAllText(filename);

            TestOneString(version, originalText, filename: filename);
        }

        public static void TestOneString(
            PythonLanguageVersion version,
            string originalText,
            CodeFormattingOptions format = null,
            string expected = null,
            bool recurse = true,
            string filename = null,
            bool verbatim = true
        ) {
            bool hadExpected = true;
            if (expected == null) {
                expected = originalText;
                hadExpected = false;
            }
            var parser = Parser.CreateParser(new StringReader(originalText), version, new ParserOptions() { Verbatim = verbatim });
            var ast = parser.ParseFile();

            string output;
            try {
                if (format == null) {
                    output = ast.ToCodeString(ast);
                } else {
                    output = ast.ToCodeString(ast, format);
                }
            } catch(Exception e) {
                Console.WriteLine($"Failed to convert to code: {0}{Environment.NewLine}{1}", originalText, e);
                Assert.Fail();
                return;
            }

            bool shownFilename = false;
            const int contextSize = 50;
            for (int i = 0; i < expected.Length && i < output.Length; i++) {
                if (expected[i] != output[i]) {
                    // output some context
                    StringBuilder x = new StringBuilder();
                    StringBuilder y = new StringBuilder();
                    StringBuilder z = new StringBuilder();
                    for (int j = Math.Max(0, i - contextSize); j < Math.Min(Math.Max(expected.Length, output.Length), i + contextSize); j++) {
                        if (j < expected.Length) {
                            x.AppendRepr(expected[j]);
                        }
                        if (j < output.Length) {
                            y.AppendRepr(output[j]);
                        }
                        if (j == i) {
                            z.Append("^");
                        } else {
                            z.Append(" ");
                        }
                    }

                    if (!shownFilename) {
                        shownFilename = true;
                        if (!string.IsNullOrEmpty(filename)) {
                            Console.WriteLine("In file: {0}", filename);
                        }
                    }
                    Console.WriteLine("Mismatch context at {0}:", i);
                    Console.WriteLine("Expected: {0}", x.ToString());
                    Console.WriteLine("Got     : {0}", y.ToString());
                    Console.WriteLine("Differs : {0}", z.ToString());

                    if (recurse) {
                        // Try and automatically get a minimal repro if we can...
                        if (!hadExpected) {
                            try {
                                for (int j = i; j >= 0; j--) {
                                    TestOneString(version, originalText.Substring(j), format, null, false);
                                }
                            } catch {
                            }
                        }
                    } else {
                        Console.WriteLine("-----");
                        Console.WriteLine(expected);
                        Console.WriteLine("-----");
                    }

                    Assert.AreEqual(expected[i], output[i], String.Format("Characters differ at {0}, got {1}, expected {2}", i, output[i], expected[i]));
                }
            }

            if (expected.Length != output.Length) {
                Console.WriteLine("Original: {0}", expected.ToString());
                Console.WriteLine("New     : {0}", output.ToString());
            }
            Assert.AreEqual(expected.Length, output.Length);
        }        
    }
}
