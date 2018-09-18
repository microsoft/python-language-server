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

using System;
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
using TestUtilities;

namespace AnalysisTests {
    [TestClass]
    public class LineFormatterTests {
        [TestMethod, Priority(0)]
        public async Task LineOutOfBounds() {
            await AssertNoEdits("a+b", line: 0);
            await AssertNoEdits("a+b", line: -1);
        }

        [TestMethod, Priority(0)]
        public async Task FormatEmpty() {
            await AssertNoEdits("");
            await AssertNoEdits("  ");
            await AssertNoEdits("\t");
        }

        [TestMethod, Priority(0)]
        public async Task OperatorSpacing() {
            await AssertSingleLineFormat("( x  +1 )*y/ 3", "(x + 1) * y / 3");
        }

        [TestMethod, Priority(0)]
        public async Task TupleComma() {
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
            await AssertSingleLineFormat("ham [lower+ offset :upper+offset]", "ham[lower + offset : upper + offset]");
        }

        [TestMethod, Priority(0)]
        public async Task ColonSlicesWithFunctions() {
            await AssertSingleLineFormat("ham[ : upper_fn ( x) : step_fn(x )], ham[ :: step_fn(x)]", "ham[: upper_fn(x) : step_fn(x)], ham[:: step_fn(x)]");
        }

        [TestMethod, Priority(0)]
        public async Task ColonInForLoop() {
            await AssertSingleLineFormat("for index in  range( len(fruits) ): ", "for index in range(len(fruits)):");
        }

        [TestMethod, Priority(0)]
        public async Task TrailingComment() {
            await AssertSingleLineFormat("x=1    # comment", "x = 1  # comment");
        }

        [TestMethod, Priority(0)]
        public async Task SingleComment() {
            await AssertSingleLineFormat("# comment");
        }

        [TestMethod, Priority(0)]
        public async Task CommentWithLeadingWhitespace() {
            await AssertSingleLineFormat("   # comment", "# comment", editStart: 4);
        }

        [TestMethod, Priority(0)]
        public async Task AsterisksArgsKwargs() {
            await AssertSingleLineFormat("foo( *a, ** b)", "foo(*a, **b)");
        }

        [TestMethod, Priority(0)]
        public async Task BraceAfterKeyword() {
            await AssertSingleLineFormat("for x in(1,2,3)", "for x in (1, 2, 3)");
            await AssertSingleLineFormat("assert(1,2,3)", "assert (1, 2, 3)");
            await AssertSingleLineFormat("if (True|False)and(False/True)and not ( x )", "if (True | False) and (False / True) and not (x)");
            await AssertSingleLineFormat("while (True|False)", "while (True | False)");
            await AssertSingleLineFormat("yield(a%b)", "yield (a % b)");
        }

        [TestMethod, Priority(0)]
        public async Task DotOperator() {
            await AssertSingleLineFormat("x.y", "x.y");
            await AssertSingleLineFormat("x. y", "x.y");
            await AssertSingleLineFormat("5 .y", "5 .y");
        }

        [TestMethod, Priority(0)]
        public async Task DoubleAsterisk() {
            await AssertSingleLineFormat("foo(a**2, **k)", "foo(a ** 2, **k)");
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

            await AssertSingleLineFormat("x = (-y)", "x = (-y)");
            await AssertSingleLineFormat("x = (+ y)", "x = (+y)");
            await AssertSingleLineFormat("x = (~ y)", "x = (~y)");
            await AssertSingleLineFormat("x =(-1)", "x = (-1)");
            await AssertSingleLineFormat("x =   (+ 1)", "x = (+1)");
            await AssertSingleLineFormat("x = ( ~1)", "x = (~1)");

            await AssertSingleLineFormat("foo(-3.14, +1, ~0xDEADBEEF)", "foo(-3.14, +1, ~0xDEADBEEF)");
            await AssertSingleLineFormat("foo(a=-3.14, b=+1, c=~0xDEADBEEF)", "foo(a=-3.14, b=+1, c=~0xDEADBEEF)");
        }

        [TestMethod, Priority(0)]
        public async Task EqualsWithTypeHints() {
            await AssertSingleLineFormat("def foo(x:int=3,x=100.)", "def foo(x: int = 3, x=100.)");
        }

        [TestMethod, Priority(0)]
        public async Task TrailingCommaAssignment() {
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
            await AssertSingleLineFormat("x = foo(\n  * param1,\n  * param2\n)", "*param1,", line: 2, editStart: 3);
            await AssertSingleLineFormat("x = foo(\n  * param1,\n  * param2\n)", "*param2", line: 3, editStart: 3);
        }

        [TestMethod, Priority(0)]
        public async Task Arrow() {
            await AssertSingleLineFormat("def f(a, \n    ** k: 11) -> 12: pass", "**k: 11) -> 12: pass", line: 2, editStart: 5);
        }

        [TestMethod, Priority(0)]
        public async Task MultilineFunctionCall() {
            await AssertSingleLineFormat("def foo(x = 1)", "def foo(x=1)", line: 1);
            await AssertSingleLineFormat("def foo(a\n, x = 1)", ", x=1)", line: 2);
            await AssertSingleLineFormat("foo(a  ,b,\n  x = 1)", "x=1)", line: 2, editStart: 3);
            await AssertSingleLineFormat("if True:\n  if False:\n    foo(a  , bar(\n      x = 1)", "x=1)", line: 4, editStart: 7);
            await AssertSingleLineFormat("z=foo (0 , x= 1, (3+7) , y , z )", "z = foo(0, x=1, (3 + 7), y, z)", line: 1);
            await AssertSingleLineFormat("foo (0,\n x= 1,", "x=1,", line: 2, editStart: 2);

            await AssertSingleLineFormat(@"async def fetch():
  async with aiohttp.ClientSession() as session:
    async with session.ws_connect(
        ""http://127.0.0.1:8000/"", headers = cookie) as ws: # add unwanted spaces", @"""http://127.0.0.1:8000/"", headers=cookie) as ws:  # add unwanted spaces", line: 4, editStart: 9);

            await AssertSingleLineFormat("def pos0key1(*, key): return key\npos0key1(key= 100)", "pos0key1(key=100)", line: 2);
            await AssertSingleLineFormat("def test_string_literals(self):\n  x= 1; y =2; self.assertTrue(len(x) == 0 and x == y)", "x = 1; y = 2; self.assertTrue(len(x) == 0 and x == y)", line: 2, editStart: 3);
        }

        [TestMethod, Priority(0)]
        public async Task RemoveTrailingSpace() {
            await AssertSingleLineFormat("a+b ", "a + b");
        }

        // https://github.com/Microsoft/vscode-python/issues/1783
        [TestMethod, Priority(0)]
        public async Task IterableUnpacking() {
            await AssertSingleLineFormat("*a, b, c = 1, 2, 3");
            await AssertSingleLineFormat("a, *b, c = 1, 2, 3");
            await AssertSingleLineFormat("a, b, *c = 1, 2, 3");
            await AssertSingleLineFormat("a, *b, = 1, 2, 3");
        }

        // https://github.com/Microsoft/vscode-python/issues/1792
        // https://www.python.org/dev/peps/pep-0008/#pet-peeves
        [TestMethod, Priority(0)]
        public async Task SlicingPetPeeves() {
            await AssertSingleLineFormat("ham[lower+offset : upper+offset]", "ham[lower + offset : upper + offset]");
            await AssertSingleLineFormat("ham[: upper_fn(x) : step_fn(x)], ham[:: step_fn(x)]", "ham[: upper_fn(x) : step_fn(x)], ham[:: step_fn(x)]");
            await AssertSingleLineFormat("ham[lower + offset : upper + offset]", "ham[lower + offset : upper + offset]");
            await AssertSingleLineFormat("ham[1: 9], ham[1 : 9], ham[1 :9 :3]", "ham[1:9], ham[1:9], ham[1:9:3]");
            await AssertSingleLineFormat("ham[lower : : upper]", "ham[lower::upper]");
            await AssertSingleLineFormat("ham[ : upper]", "ham[:upper]");
            await AssertSingleLineFormat("foo[-5:]");
            await AssertSingleLineFormat("foo[:-5]");
            await AssertSingleLineFormat("foo[+5:]");
            await AssertSingleLineFormat("foo[:+5]");
            await AssertSingleLineFormat("foo[~5:]");
            await AssertSingleLineFormat("foo[:~5]");
            await AssertSingleLineFormat("foo[-a:]");
        }

        // https://github.com/Microsoft/vscode-python/issues/1784
        [TestMethod, Priority(0)]
        public async Task LiteralFunctionCall() {
            await AssertSingleLineFormat("5 .bit_length()", "5 .bit_length()");
        }

        // https://github.com/Microsoft/vscode-python/issues/2323
        [TestMethod, Priority(0)]
        public async Task MultilineFString() {
            var text = @"f""""""
select* from { table}
where { condition}
order by { order_columns}
limit { limit_num}; """"""";

            using (var reader = new StringReader(text)) {
                var lineFormatter = new LineFormatter(reader, PythonLanguageVersion.V37);

                var edits = lineFormatter.FormatLine(5);
                edits.Should().BeEmpty();
            }
        }

        [TestMethod, Priority(0)]
        public async Task Ellipsis() {
            await AssertSingleLineFormat("x=...", "x = ...");
        }

        [TestMethod, Priority(0)]
        public async Task PEP448() {
            await AssertSingleLineFormat("print(*[1], *[2], 3)");
            await AssertSingleLineFormat("dict(**{'x': 1}, y=2, **{'z': 3})");
            await AssertSingleLineFormat("*range(4), 4");
            await AssertSingleLineFormat("[*range(4), 4]");
            await AssertSingleLineFormat("{*range(4), 4}");
            await AssertSingleLineFormat("{'x': 1, **{'y': 2}}");
            await AssertSingleLineFormat("{'x': 1, **{'x': 2}}");
            await AssertSingleLineFormat("{**{'x': 2}, 'x': 1}");
        }

        [TestMethod, Priority(0)]
        public async Task GrammarFile() {
            var src = TestData.GetPath("TestData", "Formatting", "pythonGrammar.py");

            string fileContents;
            using (var reader = new StreamReader(src, true)) {
                fileContents = await reader.ReadToEndAsync();
            }

            var lines = fileContents.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);


            using (var reader = new StringReader(fileContents)) {
                var lineFormatter = new LineFormatter(reader, PythonLanguageVersion.V37);

                for (var i = 0; i < lines.Length; i++) {
                    var lineNum = i + 1;

                    var edits = lineFormatter.FormatLine(lineNum);

                    var lineText = lines[i];
                    var lineTextOrig = lines[i];

                    foreach (var edit in edits) {
                        edit.range.start.line.Should().Be(i);
                        edit.range.end.line.Should().Be(i);

                        lineText = ApplyLineEdit(lineText, edit);
                    }

                    lineText.Should().Be(lineTextOrig, $"because line {lineNum} should be unchanged");
                }

            }
        }

        public static async Task AssertSingleLineFormat(string text, string expected = null, int line = 1, PythonLanguageVersion languageVersion = PythonLanguageVersion.V37, int editStart = 1) {
            if (text == null) {
                throw new ArgumentNullException(nameof(text));
            }

            if (expected == null) {
                expected = text;
            }

            using (var reader = new StringReader(text)) {
                var lineFormatter = new LineFormatter(reader, languageVersion);

                var edits = lineFormatter.FormatLine(line);

                edits.Should().OnlyContain(new TextEdit {
                    newText = expected,
                    range = new Range {
                        start = new SourceLocation(line, editStart),
                        end = new SourceLocation(line, text.Split('\n')[line - 1].Length + 1)
                    }
                });
            }
        }

        public static async Task AssertNoEdits(string text, int line = 1, PythonLanguageVersion languageVersion = PythonLanguageVersion.V37) {
            using (var reader = new StringReader(text)) {
                var lineFormatter = new LineFormatter(reader, languageVersion);
                lineFormatter.FormatLine(line).Should().BeEmpty();
            }
        }

        public static string ApplyLineEdit(string s, TextEdit edit) {
            if (edit.range.start.line != edit.range.end.line) {
                throw new ArgumentException("Edit should only operate on a single line", nameof(edit));
            }

            var startIndex = edit.range.start.character;
            var removeCount = edit.range.end.character - edit.range.start.character - 1;

            return s.Remove(startIndex, removeCount).Insert(startIndex, edit.newText);
        }
    }
}
