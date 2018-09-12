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

using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.LanguageServer;
using Microsoft.Python.LanguageServer.Implementation;
using Microsoft.Python.Tests.Utilities.FluentAssertions;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.FluentAssertions;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnalysisTests {
    [TestClass]
    public class LineFormatterTests {
        [TestMethod, Priority(0)]
        public async Task OperatorSpacing() {
            await AssertSingleLineFormat("( x  +1 )*y/ 3", "(x + 1) * y / 3");
        }

        [TestMethod, Priority(0)]
        public async Task BracesSpacing() {
            await AssertSingleLineFormat("foo =(0 ,)", "foo = (0,)");
        }

        [TestMethod, Priority(0)]
        public async Task ColonRegular() {
            await AssertSingleLineFormat("if x == 4 : print x,y; x,y= y, x", "if x == 4: print x, y; x, y = y, x", languageVersion: PythonLanguageVersion.V27);
        }

        [TestMethod, Priority(0)]
        public async Task ColonSlices() {
            await AssertSingleLineFormat("x[1: 30]", "x[1:30]");
        }

        [TestMethod, Priority(0)]
        public async Task ColonSlicesInArguments() {
            await AssertSingleLineFormat("spam ( ham[ 1 :3], {eggs : 2})", "spam(ham[1:3], {eggs: 2})");
        }

        [TestMethod, Priority(0)]
        public async Task ColonSlicesWithDoubleColon() {
            await AssertSingleLineFormat("ham [1:9 ], ham[ 1: 9:   3], ham[: 9 :3], ham[1: :3], ham [ 1: 9:]", "ham[1:9], ham[1:9:3], ham[:9:3], ham[1::3], ham[1:9:]");
        }

        [TestMethod, Priority(0)]
        public async Task ColonSlicesWithOperators() {
            await AssertSingleLineFormat("ham [lower+ offset :upper+offset]", "ham[lower + offset:upper + offset]");
        }

        [TestMethod, Priority(0)]
        public async Task ColonSlicesWithFunctions() {
            await AssertSingleLineFormat("ham[ : upper_fn ( x) : step_fn(x )], ham[ :: step_fn(x)]", "ham[:upper_fn(x):step_fn(x)], ham[::step_fn(x)]");
        }

        [TestMethod, Priority(0)]
        public async Task ColonInForLoop() {
            await AssertSingleLineFormat("for index in  range( len(fruits) ): ", "for index in range(len(fruits)):");
        }

        [TestMethod, Priority(0)]
        public async Task NestedBraces() {
            await AssertSingleLineFormat("[ 1 :[2: (x,),y]]{1}", "[1:[2:(x,), y]]{1}");
        }

        [TestMethod, Priority(0)]
        public async Task TrailingComment() {
            await AssertSingleLineFormat("x=1    # comment", "x = 1  # comment");
        }

        [TestMethod, Priority(0)]
        public async Task SingleComment() {
            await AssertSingleLineFormat("# comment", "# comment");
        }

        [TestMethod, Priority(0)]
        public async Task CommentWithLeadingWhitespace() {
            await AssertSingleLineFormat("   # comment", "   # comment");
        }

        [TestMethod, Priority(0)]
        public async Task OperatorsWithoutFollowingSpace() {
            await AssertSingleLineFormat("foo( *a, ** b)", "foo(*a, **b)");
        }

        [TestMethod, Priority(0)]
        public async Task BraceAfterKeyword() {
            await AssertSingleLineFormat("for x in(1,2,3)", "for x in (1, 2, 3)");
            await AssertSingleLineFormat("assert(1,2,3)", "assert (1, 2, 3)");
            await AssertSingleLineFormat("if (True|False)and(False/True)not (! x )", "if (True | False) and (False / True) not (!x)");
            await AssertSingleLineFormat("while (True|False)", "while (True | False)");
            await AssertSingleLineFormat("yield(a%b)", "yield (a % b)");
        }

        [TestMethod, Priority(0)]
        public async Task DotOperator() {
            await AssertSingleLineFormat("x.y", "x.y");
            await AssertSingleLineFormat("x. y", "x.y");
            await AssertSingleLineFormat("5 .y", "5.y");
        }

        [TestMethod, Priority(0)]
        public async Task DoubleAsterisk() {
            await AssertSingleLineFormat("a**2, **k", "a ** 2, **k");
        }

        [TestMethod, Priority(0)]
        public async Task Lambda() {
            await AssertSingleLineFormat("lambda * args, :0", "lambda *args,: 0");
        }

        [TestMethod, Priority(0)]
        public async Task CommaExpression() {
            await AssertSingleLineFormat("x=1,2,3", "x = 1, 2, 3");
        }

        [TestMethod, Priority(0)]
        public async Task IsExpression() {
            await AssertSingleLineFormat("a( (False is  2)  is 3)", "a((False is 2) is 3)");
        }

        [TestMethod, Priority(0)]
        public async Task FunctionReturningTuple() {
            await AssertSingleLineFormat("x,y=f(a)", "x, y = f(a)");
        }

        [TestMethod, Priority(0)]
        public async Task FromDotImport() {
            await AssertSingleLineFormat("from. import A", "from . import A");
        }

        [TestMethod, Priority(0)]
        public async Task FromDotDotImport() {
            await AssertSingleLineFormat("from ..import A", "from .. import A");
        }

        [TestMethod, Priority(0)]
        public async Task FromDotDotXImport() {
            await AssertSingleLineFormat("from..x import A", "from ..x import A");
        }

        [TestMethod, Priority(0)]
        public async Task RawStrings() {
            await AssertSingleLineFormat("z=r\"\"", "z = r\"\"");
            await AssertSingleLineFormat("z=rf\"\"", "z = rf\"\"");
            await AssertSingleLineFormat("z=R\"\"", "z = R\"\"");
            await AssertSingleLineFormat("z=RF\"\"", "z = RF\"\"");
        }

        [TestMethod, Priority(0)]
        public async Task UnaryOperators() {
            await AssertSingleLineFormat("x = - y", "x = -y");
            await AssertSingleLineFormat("x = + y", "x = +y");
            await AssertSingleLineFormat("x = ~ y", "x = ~y");
            await AssertSingleLineFormat("x =-1", "x = -1");
            await AssertSingleLineFormat("x =   +1", "x = +1");
            await AssertSingleLineFormat("x =  ~1", "x = ~1");
        }

        [TestMethod, Priority(0)]
        public async Task EqualsWithTypeHints() {
            await AssertSingleLineFormat("def foo(x:int=3,x=100.)", "def foo(x: int = 3, x=100.)");
        }

        [TestMethod, Priority(0)]
        public async Task TrailingComma() {
            await AssertSingleLineFormat("a, =[1]", "a, = [1]");
        }

        [TestMethod, Priority(0)]
        public async Task IfTrue() {
            await AssertSingleLineFormat("if(True) :", "if (True):");
        }

        [TestMethod, Priority(0)]
        public async Task LambdaArguments() {
            await AssertSingleLineFormat("l4= lambda x =lambda y =lambda z= 1: z: y(): x()", "l4 = lambda x=lambda y=lambda z=1: z: y(): x()");
        }

        [TestMethod, Priority(0)]
        public async Task StarInMultilineArguments() {
            await AssertSingleLineFormat("x = [\n  * param1,\n  * param2\n]", "  *param1,", line: 2);
            await AssertSingleLineFormat("x = [\n  * param1,\n  * param2\n]", "  *param2,", line: 3);
        }

        [TestMethod, Priority(0)]
        public async Task ArrowOperator() {
            await AssertSingleLineFormat("def f(a, \n    ** k: 11) -> 12: pass", "    **k: 11) -> 12: pass", line: 2);
        }

        [TestMethod, Priority(0)]
        public async Task MultilineFunctionCall() {
            await AssertSingleLineFormat("def foo(x = 1)", "def foo(x=1)", line: 1);
            await AssertSingleLineFormat("def foo(a\n, x = 1)", ", x=1)", line: 2);
            await AssertSingleLineFormat("foo(a  ,b,\n  x = 1)", "  x=1)", line: 2);
            await AssertSingleLineFormat("if True:\n  if False:\n    foo(a  , bar(\n      x = 1)", "      x=1)", line: 4);
            await AssertSingleLineFormat("z=foo (0 , x= 1, (3+7) , y , z )", "z = foo(0, x=1, (3 + 7), y, z)", line: 1);
            await AssertSingleLineFormat("foo (0,\n x= 1,", " x=1,", line: 2);

            await AssertSingleLineFormat(@"async def fetch():
  async with aiohttp.ClientSession() as session:
    async with session.ws_connect(
        ""http://127.0.0.1:8000/\"", headers = cookie) as ws: # add unwanted spaces", @"        ""http://127.0.0.1:8000/"", headers=cookie) as ws:  # add unwanted spaces", line: 4);

            await AssertSingleLineFormat("def pos0key1(*, key): return key\npos0key1(key= 100)", "pos0key1(key=100)", line: 2);
            await AssertSingleLineFormat("def test_string_literals(self):\n  x= 1; y =2; self.assertTrue(len(x) == 0 and x == y)", "  x = 1; y = 2; self.assertTrue(len(x) == 0 and x == y)", line: 2);
        }

        [TestMethod, Priority(0)]
        public async Task GrammarFile() {
            true.Should().BeFalse();
        }

        public static async Task AssertSingleLineFormat(string text, string expected, int line = 1, PythonLanguageVersion languageVersion = PythonLanguageVersion.V37) {
            using (var reader = new StringReader(text)) {
                var lineFormatter = new LineFormatter(reader, languageVersion);

                var edits = lineFormatter.FormatLine(line);

                edits.Should().OnlyContain(new TextEdit {
                    newText = expected,
                    range = new Range {
                        start = new SourceLocation(1, 1),
                        end = new SourceLocation(1, text.Length + 1)
                    }
                });
            }
        }
    }
}
