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
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace AnalysisTests {
    [TestClass]
    public class LineFormatterTests {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() {
            TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");
        }

        [TestCleanup]
        public void TestCleanup() {
            TestEnvironmentImpl.TestCleanup();
        }

        [TestMethod, Priority(0)]
        public void LineOutOfBounds() {
            AssertNoEdits("a+b", line: -1);
            AssertNoEdits("a+b", line: 1);
        }

        [DataRow("")]
        [DataRow("  ")]
        [DataRow("\t")]
        [DataTestMethod, Priority(0)]
        public void FormatEmpty(string code) {
            AssertNoEdits(code);
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
            AssertSingleLineFormat("# comment", "# comment");
        }

        [TestMethod, Priority(0)]
        public void CommentWithLeadingWhitespace() {
            AssertSingleLineFormat("   # comment", "# comment", editStart: 3);
        }

        [TestMethod, Priority(0)]
        public void AsterisksArgsKwargs() {
            AssertSingleLineFormat("foo( *a, ** b)", "foo(*a, **b)");
        }

        [DataRow("for x in(1,2,3)", "for x in (1, 2, 3)")]
        [DataRow("assert(1,2,3)", "assert (1, 2, 3)")]
        [DataRow("if (True|False)and(False/True)and not ( x )", "if (True | False) and (False / True) and not (x)")]
        [DataRow("while (True|False)", "while (True | False)")]
        [DataRow("yield(a%b)", "yield (a % b)")]
        [DataTestMethod, Priority(0)]
        public void BraceAfterKeyword(string code, string expected) {
            AssertSingleLineFormat(code, expected);
        }

        [DataRow("x.y", "x.y")]
        [DataRow("x. y", "x.y")]
        [DataRow("5 .y", "5 .y")]
        [DataTestMethod, Priority(0)]
        public void DotOperator(string code, string expected) {
            AssertSingleLineFormat(code, expected);
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

        [DataRow("z=r\"\"", "z = r\"\"")]
        [DataRow("z=rf\"\"", "z = rf\"\"")]
        [DataRow("z=R\"\"", "z = R\"\"")]
        [DataRow("z=RF\"\"", "z = RF\"\"")]
        [DataTestMethod, Priority(0)]
        public void RawStrings(string code, string expected) {
            AssertSingleLineFormat(code, expected);
        }

        [DataRow("x = - y", "x = -y")]
        [DataRow("x = + y", "x = +y")]
        [DataRow("x = ~ y", "x = ~y")]
        [DataRow("x =-1", "x = -1")]
        [DataRow("x =   +1", "x = +1")]
        [DataRow("x =  ~1", "x = ~1")]
        [DataRow("x = (-y)", "x = (-y)")]
        [DataRow("x = (+ y)", "x = (+y)")]
        [DataRow("x = (~ y)", "x = (~y)")]
        [DataRow("x =(-1)", "x = (-1)")]
        [DataRow("x =   (+ 1)", "x = (+1)")]
        [DataRow("x = ( ~1)", "x = (~1)")]
        [DataRow("foo(-3.14, +1, ~0xDEADBEEF)", "foo(-3.14, +1, ~0xDEADBEEF)")]
        [DataRow("foo(a=-3.14, b=+1, c=~0xDEADBEEF)", "foo(a=-3.14, b=+1, c=~0xDEADBEEF)")]
        [DataTestMethod, Priority(0)]
        public void UnaryOperators(string code, string expected) {
            AssertSingleLineFormat(code, expected);
        }

        [DataRow("def foo(x:int=3,x=100.)", "def foo(x: int = 3, x=100.)")]
        [DataRow("def foo(x:Union[int,str]=3,x=100.)", "def foo(x: Union[int, str] = 3, x=100.)")]
        [DataTestMethod, Priority(0)]
        public void EqualsWithTypeHints(string code, string expected) {
            AssertSingleLineFormat(code, expected);
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

        [DataRow("x = foo(\n  * param1,\n  * param2\n)", "*param1,", 1, 2)]
        [DataRow("x = foo(\n  * param1,\n  * param2\n)", "*param2", 2, 2)]
        [DataTestMethod, Priority(0)]
        public void StarInMultilineArguments(string code, string expected, int line, int editStart) {
            AssertSingleLineFormat(code, expected, line: line, editStart: editStart);
        }

        [TestMethod, Priority(0)]
        public void Arrow() {
            AssertSingleLineFormat("def f(a, \n    ** k: 11) -> 12: pass", "**k: 11) -> 12: pass", line: 1, editStart: 4);
        }

        [DataRow("def foo(x = 1)", "def foo(x=1)", 0, 0)]
        [DataRow("def foo(a\n, x = 1)", ", x=1)", 1, 0)]
        [DataRow("foo(a  ,b,\n  x = 1)", "x=1)", 1, 2)]
        [DataRow("if True:\n  if False:\n    foo(a  , bar(\n      x = 1)", "x=1)", 3, 6)]
        [DataRow("z=foo (0 , x= 1, (3+7) , y , z )", "z = foo(0, x=1, (3 + 7), y, z)", 0, 0)]
        [DataRow("foo (0,\n x= 1,", "x=1,", 1, 1)]
        [DataRow(@"async def fetch():
  async with aiohttp.ClientSession() as session:
    async with session.ws_connect(
        ""http://127.0.0.1:8000/"", headers = cookie) as ws: # add unwanted spaces", @"""http://127.0.0.1:8000/"", headers=cookie) as ws:  # add unwanted spaces", 3, 8)]
        [DataRow("def pos0key1(*, key): return key\npos0key1(key= 100)", "pos0key1(key=100)", 1, 0)]
        [DataRow("def test_string_literals(self):\n  x= 1; y =2; self.assertTrue(len(x) == 0 and x == y)", "x = 1; y = 2; self.assertTrue(len(x) == 0 and x == y)", 1, 2)]
        [DataTestMethod, Priority(0)]
        public void MultilineFunctionCall(string code, string expected, int line, int editStart) {
            AssertSingleLineFormat(code, expected, line: line, editStart: editStart);
        }

        [TestMethod, Priority(0)]
        public void RemoveTrailingSpace() {
            AssertSingleLineFormat("a+b ", "a + b");
        }

        // https://github.com/Microsoft/vscode-python/issues/1783
        [DataRow("*a, b, c = 1, 2, 3")]
        [DataRow("a, *b, c = 1, 2, 3")]
        [DataRow("a, b, *c = 1, 2, 3")]
        [DataRow("a, *b, = 1, 2, 3")]
        [DataTestMethod, Priority(0)]
        public void IterableUnpacking(string code) {
            AssertSingleLineFormat(code, code);
        }

        // https://github.com/Microsoft/vscode-python/issues/1792
        // https://www.python.org/dev/peps/pep-0008/#pet-peeves
        [DataRow("ham[lower+offset : upper+offset]", "ham[lower + offset : upper + offset]")]
        [DataRow("ham[: upper_fn(x) : step_fn(x)], ham[:: step_fn(x)]", "ham[: upper_fn(x) : step_fn(x)], ham[:: step_fn(x)]")]
        [DataRow("ham[lower + offset : upper + offset]", "ham[lower + offset : upper + offset]")]
        [DataRow("ham[1: 9], ham[1 : 9], ham[1 :9 :3]", "ham[1:9], ham[1:9], ham[1:9:3]")]
        [DataRow("ham[lower : : upper]", "ham[lower::upper]")]
        [DataRow("ham[ : upper]", "ham[:upper]")]
        [DataRow("foo[-5:]", "foo[-5:]")]
        [DataRow("foo[:-5]", "foo[:-5]")]
        [DataRow("foo[+5:]", "foo[+5:]")]
        [DataRow("foo[:+5]", "foo[:+5]")]
        [DataRow("foo[~5:]", "foo[~5:]")]
        [DataRow("foo[:~5]", "foo[:~5]")]
        [DataRow("foo[-a:]", "foo[-a:]")]
        [DataTestMethod, Priority(0)]
        public void SlicingPetPeeves(string code, string expected) {
            AssertSingleLineFormat(code, expected);
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
            AssertNoEdits(@"f""""""
select* from { table}
where { condition}
order by { order_columns}
limit { limit_num}; """"""", line: 5);
        }

        [TestMethod, Priority(0)]
        public void Ellipsis() {
            AssertSingleLineFormat("x=...", "x = ...");
        }

        [DataRow("print(*[1], *[2], 3)")]
        [DataRow("dict(**{'x': 1}, y=2, **{'z': 3})")]
        [DataRow("*range(4), 4")]
        [DataRow("[*range(4), 4]")]
        [DataRow("{*range(4), 4}")]
        [DataRow("{'x': 1, **{'y': 2}}")]
        [DataRow("{'x': 1, **{'x': 2}}")]
        [DataRow("{**{'x': 2}, 'x': 1}")]
        [DataTestMethod, Priority(0)]
        public void PEP448(string code) {
            AssertSingleLineFormat(code, code);
        }

        [TestMethod, Priority(0)]
        public void MultilineStringAssignment() {
            AssertSingleLineFormat("x='''abc\ntest'''abc", "x = '''abc");
        }

        [TestMethod, Priority(0)]
        public void MultilineDefaultArg() {
            AssertSingleLineFormat("def foo(x='''abc\ntest''')", "def foo(x='''abc");
        }

        [TestMethod, Priority(0)]
        public void LineContinuation() {
            AssertSingleLineFormat("a+b+ \\\n", "a + b + \\");
        }

        [DataRow("foo.a() \\\n   .b() \\\n   .c()", "foo.a() \\", 0, 0, 9)]
        [DataRow("foo.a() \\\r\n   .b() \\\r\n   .c()", "foo.a() \\", 0, 0, 9)]
        [DataRow("foo.a() \\\n   .b() \\\n   .c()", ".b() \\", 1, 3, 9)]
        [DataRow("foo.a() \\\r\n   .b() \\\r\n   .c()", ".b() \\", 1, 3, 9)]
        [DataRow("foo.a() \\\n   .b() \\\n   .c()", ".c()", 2, 3, 7)]
        [DataRow("foo.a() \\\r\n   .b() \\\r\n   .c()", ".c()", 2, 3, 7)]
        [DataTestMethod, Priority(0)]
        public void MultilineChainedCall(string code, string expected, int line, int characterStart, int characterEnd) {
            var edits = new LineFormatter(new StringReader(code), PythonLanguageVersion.V36).FormatLine(line);
            edits.Should().OnlyHaveTextEdit(expected, (line, characterStart, line, characterEnd));
        }

        [DataRow("a[:, :, :, 1]")]
        [DataRow("a[x:y, x + 1 :y, :, 1]")]
        [DataRow("a[:, 1:3]")]
        [DataRow("a[:, :3, :]")]
        [DataRow("a[:, 3:, :]")]
        [DataTestMethod, Priority(0)]
        public void BracketCommas(string code) {
            AssertSingleLineFormat(code, code);
        }

        [TestMethod, Priority(0)]
        public void MultilineStringTrailingComment() {
            AssertSingleLineFormat("'''\nfoo\n''' # comment", "  # comment", line: 2, editStart: 3);
        }

        [DataRow("`a`")]
        [DataRow("foo(`a`)")]
        [DataRow("`a` if a else 'oops'")]
        [DataTestMethod, Priority(0)]
        public void Backtick(string code) {
            AssertSingleLineFormat(code, code, languageVersion: PythonLanguageVersion.V27);
        }

        [DataRow("exec code", PythonLanguageVersion.V27)]
        [DataRow("exec (code)", PythonLanguageVersion.V27)]
        [DataRow("exec(code)", PythonLanguageVersion.V37)]
        [DataTestMethod, Priority(0)]
        public void ExecStatement(string code, PythonLanguageVersion version) {
            AssertSingleLineFormat(code, code, languageVersion: version);
        }

        [TestMethod, Priority(0)]
        public void CommentAfterOperator() {
            AssertSingleLineFormat("a+# comment\nb", "a +  # comment");
        }

        [DataRow("'a''b'", "'a' 'b'")]
        [DataRow("'a' 'b'", "'a' 'b'")]
        [DataRow("'''a''''''b'''", "'''a''' '''b'''")]
        [DataRow("'''a'''r'''b'''", "'''a''' r'''b'''")]
        [DataRow("\"a\"\"b\"", "\"a\" \"b\"")]
        [DataRow("\"a\" \"b\"", "\"a\" \"b\"")]
        [DataRow("\"\"\"a\"\"\"\"\"\"b\"\"\"", "\"\"\"a\"\"\" \"\"\"b\"\"\"")]
        [DataRow("\"\"\"a\"\"\"r\"\"\"b\"\"\"", "\"\"\"a\"\"\" r\"\"\"b\"\"\"")]
        [DataTestMethod, Priority(0)]
        public void StringConcat(string code, string expected) {
            AssertSingleLineFormat(code, expected);
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
                    var edits = lineFormatter.FormatLine(i);
                    edits.Should().NotBeNull().And.HaveCountLessOrEqualTo(1);

                    if (edits.Length == 0) {
                        continue;
                    }

                    var edit = edits[0];
                    var start = edit.range.start;
                    var end = edit.range.end;

                    start.line.Should().Be(i);
                    end.line.Should().Be(i);

                    var lineText = lines[i];
                    edit.newText.Should().Be(lineText.Substring(start.character, end.character - start.character), $"because line {i} should be unchanged");
                }
            }
        }

        /// <summary>
        /// Checks that a single line of input text is formatted as expected.
        /// </summary>
        /// <param name="text">Input code to format.</param>
        /// <param name="expected">The expected result from the formatter.</param>
        /// <param name="line">The line number to request to be formatted.</param>
        /// <param name="languageVersion">Python language version to format.</param>
        /// <param name="editStart">Where the edit should begin (i.e. when whitespace or a multi-line string begins a line).</param>
        public static void AssertSingleLineFormat(string text, string expected, int line = 0, PythonLanguageVersion languageVersion = PythonLanguageVersion.V37, int editStart = 0) {
            Check.ArgumentNull(nameof(text), text);
            Check.ArgumentNull(nameof(expected), expected);

            using (var reader = new StringReader(text)) {
                var edits = new LineFormatter(reader, languageVersion).FormatLine(line);
                edits.Should().OnlyHaveTextEdit(expected, (line, editStart, line, text.Split('\n')[line].Length));
            }
        }

        public static void AssertNoEdits(string text, int line = 0, PythonLanguageVersion languageVersion = PythonLanguageVersion.V37) {
            Check.ArgumentNull(nameof(text), text);

            using (var reader = new StringReader(text)) {
                var edits = new LineFormatter(reader, languageVersion).FormatLine(line);
                edits.Should().BeEmpty();
            }
        }
    }
}
