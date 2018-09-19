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
        public void LineOutOfBounds() {
            AssertNoEdits("a+b", line: 0);
            AssertNoEdits("a+b", line: -1);
        }

        [TestMethod, Priority(0)]
        public void FormatEmpty() {
            AssertNoEdits("");
            AssertNoEdits("  ");
            AssertNoEdits("\t");
        }

        [TestMethod, Priority(0)]
        public void OperatorSpacing() {
            AssertSingleLineFormat("( x  +1 )*y/ 3", "(x + 1) * y / 3");
        }

        [TestMethod, Priority(0)]
        public void TupleComma() {
            AssertSingleLineFormat("foo =(0 ,)", "foo = (0,)");
        }

        [TestMethod, Priority(0)]
        public void ColonRegular() {
            AssertSingleLineFormat("if x == 4 : print x,y; x,y= y, x", "if x == 4: print x, y; x, y = y, x", languageVersion: PythonLanguageVersion.V27);
        }

        [TestMethod, Priority(0)]
        public void ColonSlices() {
            AssertSingleLineFormat("x[1: 30]", "x[1:30]");
        }

        [TestMethod, Priority(0)]
        public void ColonSlicesInArguments() {
            AssertSingleLineFormat("spam ( ham[ 1 :3], {eggs : 2})", "spam(ham[1:3], {eggs: 2})");
        }

        [TestMethod, Priority(0)]
        public void ColonSlicesWithDoubleColon() {
            AssertSingleLineFormat("ham [1:9 ], ham[ 1: 9:   3], ham[: 9 :3], ham[1: :3], ham [ 1: 9:]", "ham[1:9], ham[1:9:3], ham[:9:3], ham[1::3], ham[1:9:]");
        }

        [TestMethod, Priority(0)]
        public void ColonSlicesWithOperators() {
            AssertSingleLineFormat("ham [lower+ offset :upper+offset]", "ham[lower + offset : upper + offset]");
        }

        [TestMethod, Priority(0)]
        public void ColonSlicesWithFunctions() {
            AssertSingleLineFormat("ham[ : upper_fn ( x) : step_fn(x )], ham[ :: step_fn(x)]", "ham[: upper_fn(x) : step_fn(x)], ham[:: step_fn(x)]");
        }

        [TestMethod, Priority(0)]
        public void ColonInForLoop() {
            AssertSingleLineFormat("for index in  range( len(fruits) ): ", "for index in range(len(fruits)):");
        }

        [TestMethod, Priority(0)]
        public void TrailingComment() {
            AssertSingleLineFormat("x=1    # comment", "x = 1  # comment");
        }

        [TestMethod, Priority(0)]
        public void SingleComment() {
            AssertSingleLineFormat("# comment");
        }

        [TestMethod, Priority(0)]
        public void CommentWithLeadingWhitespace() {
            AssertSingleLineFormat("   # comment", "# comment", editStart: 4);
        }

        [TestMethod, Priority(0)]
        public void AsterisksArgsKwargs() {
            AssertSingleLineFormat("foo( *a, ** b)", "foo(*a, **b)");
        }

        [TestMethod, Priority(0)]
        public void BraceAfterKeyword() {
            AssertSingleLineFormat("for x in(1,2,3)", "for x in (1, 2, 3)");
            AssertSingleLineFormat("assert(1,2,3)", "assert (1, 2, 3)");
            AssertSingleLineFormat("if (True|False)and(False/True)and not ( x )", "if (True | False) and (False / True) and not (x)");
            AssertSingleLineFormat("while (True|False)", "while (True | False)");
            AssertSingleLineFormat("yield(a%b)", "yield (a % b)");
        }

        [TestMethod, Priority(0)]
        public void DotOperator() {
            AssertSingleLineFormat("x.y", "x.y");
            AssertSingleLineFormat("x. y", "x.y");
            AssertSingleLineFormat("5 .y", "5 .y");
        }

        [TestMethod, Priority(0)]
        public void DoubleAsterisk() {
            AssertSingleLineFormat("foo(a**2, **k)", "foo(a ** 2, **k)");
        }

        [TestMethod, Priority(0)]
        public void Lambda() {
            AssertSingleLineFormat("lambda * args, :0", "lambda *args,: 0");
        }

        [TestMethod, Priority(0)]
        public void CommaExpression() {
            AssertSingleLineFormat("x=1,2,3", "x = 1, 2, 3");
        }

        [TestMethod, Priority(0)]
        public void IsExpression() {
            AssertSingleLineFormat("a( (False is  2)  is 3)", "a((False is 2) is 3)");
        }

        [TestMethod, Priority(0)]
        public void FunctionReturningTuple() {
            AssertSingleLineFormat("x,y=f(a)", "x, y = f(a)");
        }

        [TestMethod, Priority(0)]
        public void FromDotImport() {
            AssertSingleLineFormat("from. import A", "from . import A");
        }

        [TestMethod, Priority(0)]
        public void FromDotDotImport() {
            AssertSingleLineFormat("from ..import A", "from .. import A");
        }

        [TestMethod, Priority(0)]
        public void FromDotDotXImport() {
            AssertSingleLineFormat("from..x import A", "from ..x import A");
        }

        [TestMethod, Priority(0)]
        public void RawStrings() {
            AssertSingleLineFormat("z=r\"\"", "z = r\"\"");
            AssertSingleLineFormat("z=rf\"\"", "z = rf\"\"");
            AssertSingleLineFormat("z=R\"\"", "z = R\"\"");
            AssertSingleLineFormat("z=RF\"\"", "z = RF\"\"");
        }

        [TestMethod, Priority(0)]
        public void UnaryOperators() {
            AssertSingleLineFormat("x = - y", "x = -y");
            AssertSingleLineFormat("x = + y", "x = +y");
            AssertSingleLineFormat("x = ~ y", "x = ~y");
            AssertSingleLineFormat("x =-1", "x = -1");
            AssertSingleLineFormat("x =   +1", "x = +1");
            AssertSingleLineFormat("x =  ~1", "x = ~1");

            AssertSingleLineFormat("x = (-y)", "x = (-y)");
            AssertSingleLineFormat("x = (+ y)", "x = (+y)");
            AssertSingleLineFormat("x = (~ y)", "x = (~y)");
            AssertSingleLineFormat("x =(-1)", "x = (-1)");
            AssertSingleLineFormat("x =   (+ 1)", "x = (+1)");
            AssertSingleLineFormat("x = ( ~1)", "x = (~1)");

            AssertSingleLineFormat("foo(-3.14, +1, ~0xDEADBEEF)", "foo(-3.14, +1, ~0xDEADBEEF)");
            AssertSingleLineFormat("foo(a=-3.14, b=+1, c=~0xDEADBEEF)", "foo(a=-3.14, b=+1, c=~0xDEADBEEF)");
        }

        [TestMethod, Priority(0)]
        public void EqualsWithTypeHints() {
            AssertSingleLineFormat("def foo(x:int=3,x=100.)", "def foo(x: int = 3, x=100.)");
        }

        [TestMethod, Priority(0)]
        public void TrailingCommaAssignment() {
            AssertSingleLineFormat("a, =[1]", "a, = [1]");
        }

        [TestMethod, Priority(0)]
        public void IfTrue() {
            AssertSingleLineFormat("if(True) :", "if (True):");
        }

        [TestMethod, Priority(0)]
        public void LambdaArguments() {
            AssertSingleLineFormat("l4= lambda x =lambda y =lambda z= 1: z: y(): x()", "l4 = lambda x=lambda y=lambda z=1: z: y(): x()");
        }

        [TestMethod, Priority(0)]
        public void StarInMultilineArguments() {
            AssertSingleLineFormat("x = foo(\n  * param1,\n  * param2\n)", "*param1,", line: 2, editStart: 3);
            AssertSingleLineFormat("x = foo(\n  * param1,\n  * param2\n)", "*param2", line: 3, editStart: 3);
        }

        [TestMethod, Priority(0)]
        public void Arrow() {
            AssertSingleLineFormat("def f(a, \n    ** k: 11) -> 12: pass", "**k: 11) -> 12: pass", line: 2, editStart: 5);
        }

        [TestMethod, Priority(0)]
        public void MultilineFunctionCall() {
            AssertSingleLineFormat("def foo(x = 1)", "def foo(x=1)", line: 1);
            AssertSingleLineFormat("def foo(a\n, x = 1)", ", x=1)", line: 2);
            AssertSingleLineFormat("foo(a  ,b,\n  x = 1)", "x=1)", line: 2, editStart: 3);
            AssertSingleLineFormat("if True:\n  if False:\n    foo(a  , bar(\n      x = 1)", "x=1)", line: 4, editStart: 7);
            AssertSingleLineFormat("z=foo (0 , x= 1, (3+7) , y , z )", "z = foo(0, x=1, (3 + 7), y, z)", line: 1);
            AssertSingleLineFormat("foo (0,\n x= 1,", "x=1,", line: 2, editStart: 2);

            AssertSingleLineFormat(@"async def fetch():
  async with aiohttp.ClientSession() as session:
    async with session.ws_connect(
        ""http://127.0.0.1:8000/"", headers = cookie) as ws: # add unwanted spaces", @"""http://127.0.0.1:8000/"", headers=cookie) as ws:  # add unwanted spaces", line: 4, editStart: 9);

            AssertSingleLineFormat("def pos0key1(*, key): return key\npos0key1(key= 100)", "pos0key1(key=100)", line: 2);
            AssertSingleLineFormat("def test_string_literals(self):\n  x= 1; y =2; self.assertTrue(len(x) == 0 and x == y)", "x = 1; y = 2; self.assertTrue(len(x) == 0 and x == y)", line: 2, editStart: 3);
        }

        [TestMethod, Priority(0)]
        public void RemoveTrailingSpace() {
            AssertSingleLineFormat("a+b ", "a + b");
        }

        // https://github.com/Microsoft/vscode-python/issues/1783
        [TestMethod, Priority(0)]
        public void IterableUnpacking() {
            AssertSingleLineFormat("*a, b, c = 1, 2, 3");
            AssertSingleLineFormat("a, *b, c = 1, 2, 3");
            AssertSingleLineFormat("a, b, *c = 1, 2, 3");
            AssertSingleLineFormat("a, *b, = 1, 2, 3");
        }

        // https://github.com/Microsoft/vscode-python/issues/1792
        // https://www.python.org/dev/peps/pep-0008/#pet-peeves
        [TestMethod, Priority(0)]
        public void SlicingPetPeeves() {
            AssertSingleLineFormat("ham[lower+offset : upper+offset]", "ham[lower + offset : upper + offset]");
            AssertSingleLineFormat("ham[: upper_fn(x) : step_fn(x)], ham[:: step_fn(x)]", "ham[: upper_fn(x) : step_fn(x)], ham[:: step_fn(x)]");
            AssertSingleLineFormat("ham[lower + offset : upper + offset]", "ham[lower + offset : upper + offset]");
            AssertSingleLineFormat("ham[1: 9], ham[1 : 9], ham[1 :9 :3]", "ham[1:9], ham[1:9], ham[1:9:3]");
            AssertSingleLineFormat("ham[lower : : upper]", "ham[lower::upper]");
            AssertSingleLineFormat("ham[ : upper]", "ham[:upper]");
            AssertSingleLineFormat("foo[-5:]");
            AssertSingleLineFormat("foo[:-5]");
            AssertSingleLineFormat("foo[+5:]");
            AssertSingleLineFormat("foo[:+5]");
            AssertSingleLineFormat("foo[~5:]");
            AssertSingleLineFormat("foo[:~5]");
            AssertSingleLineFormat("foo[-a:]");
        }

        [TestMethod, Priority(0)]
        public void SlicingMultilineNonSimple() {
            AssertSingleLineFormat("arr[:foo\n\n\n\n.bar]", "arr[: foo");
        }

        // https://github.com/Microsoft/vscode-python/issues/1784
        [TestMethod, Priority(0)]
        public void LiteralFunctionCall() {
            AssertSingleLineFormat("5 .bit_length()", "5 .bit_length()");
        }

        // https://github.com/Microsoft/vscode-python/issues/2323
        [TestMethod, Priority(0)]
        public void MultilineFString() {
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
        public void Ellipsis() {
            AssertSingleLineFormat("x=...", "x = ...");
        }

        [TestMethod, Priority(0)]
        public void PEP448() {
            AssertSingleLineFormat("print(*[1], *[2], 3)");
            AssertSingleLineFormat("dict(**{'x': 1}, y=2, **{'z': 3})");
            AssertSingleLineFormat("*range(4), 4");
            AssertSingleLineFormat("[*range(4), 4]");
            AssertSingleLineFormat("{*range(4), 4}");
            AssertSingleLineFormat("{'x': 1, **{'y': 2}}");
            AssertSingleLineFormat("{'x': 1, **{'x': 2}}");
            AssertSingleLineFormat("{**{'x': 2}, 'x': 1}");
        }

        [TestMethod, Priority(0)]
        public void MultilineStringAssignment() {
            AssertSingleLineFormat("x='''\ntest'''", "x = ", editEnd: 3);
        }

        [TestMethod, Priority(0)]
        public void MultilineDefaultArg() {
            AssertSingleLineFormat("def foo(x='''\ntest''')", "def foo(x=", editEnd: 11);
        }

        [TestMethod, Priority(0)]
        public void GrammarFile() {
            var src = TestData.GetPath("TestData", "Formatting", "pythonGrammar.py");

            string fileContents;
            using (var reader = new StreamReader(src, true)) {
                fileContents = reader.ReadToEnd();
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

        /// <summary>
        /// Checks that a single line of input text is formatted as expected.
        /// </summary>
        /// <param name="text">Input code to format</param>
        /// <param name="expected">The expected result from the formatter. If null, then text is used.</param>
        /// <param name="line">The line number to request to be formatted.</param>
        /// <param name="languageVersion">Python language version to format.</param>
        /// <param name="editStart">Where the edit should begin (i.e. when whitespace or a multi-line string begins a line).</param>
        public static void AssertSingleLineFormat(string text, string expected = null, int line = 1, PythonLanguageVersion languageVersion = PythonLanguageVersion.V37, int editStart = 1, int? editEnd = null) {
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
                        end = new SourceLocation(line, editEnd ?? text.Split('\n')[line - 1].Length + 1)
                    }
                });
            }
        }

        public static void AssertNoEdits(string text, int line = 1, PythonLanguageVersion languageVersion = PythonLanguageVersion.V37) {
            if (text == null) {
                throw new ArgumentNullException(nameof(text));
            }

            using (var reader = new StringReader(text)) {
                var lineFormatter = new LineFormatter(reader, languageVersion);
                lineFormatter.FormatLine(line).Should().BeEmpty();
            }
        }

        public static string ApplyLineEdit(string s, TextEdit edit) {
            if (s == null) {
                throw new ArgumentNullException(nameof(s));
            }

            if (edit.range.start.line != edit.range.end.line) {
                throw new ArgumentException("Edit should only operate on a single line", nameof(edit));
            }

            var startIndex = edit.range.start.character;
            var removeCount = edit.range.end.character - edit.range.start.character - 1;

            return s.Remove(startIndex, removeCount).Insert(startIndex, edit.newText);
        }
    }
}
