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
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace AnalysisTests {
    [TestClass]
    public class BlockFormatterTests {
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
        public void NullReader() {
            Func<Task<TextEdit[]>> func = () => BlockFormatter.ProvideEdits(null, new Position(), new FormattingOptions());
            func.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("reader");
        }

        [TestMethod, Priority(0)]
        public async Task FirstLine() {
            using (var reader = new StringReader("")) {
                var edits = await BlockFormatter.ProvideEdits(reader, new Position { line = 0, character = 4 }, new FormattingOptions());
                edits.Should().BeEmpty();
            }
        }

        [TestMethod, Priority(0)]
        public void TooShort() {
            using (var reader = new StringReader("a + b")) {
                Func<Task<TextEdit[]>> func = () => BlockFormatter.ProvideEdits(reader, new Position { line = 1, character = 4 }, new FormattingOptions());
                func.Should().Throw<ArgumentException>().And.ParamName.Should().Be("position");
            }
        }

        [TestMethod, Priority(0)]
        public async Task NoMatch() {
            var code = @"d = {
    'a': a,
    'b':
";
            using (var reader = new StringReader(code)) {
                var edits = await BlockFormatter.ProvideEdits(reader, new Position { line = 2, character = 7 }, new FormattingOptions());
                edits.Should().BeEmpty();
            }
        }


        [DataRow("elseBlocksFirstLine2.py", 3, 7, true, 2, 0, 2)]
        [DataRow("elseBlocksFirstLine4.py", 3, 9, true, 4, 0, 4)]
        [DataRow("elseBlocksFirstLineTab.py", 3, 6, false, 4, 0, 1)]
        [DataTestMethod, Priority(0)]
        public async Task ElseBlock(string filename, int line, int col, bool insertSpaces, int tabSize, int startCharacter, int endCharacter) {
            var position = new Position { line = line, character = col };
            var options = new FormattingOptions { insertSpaces = insertSpaces, tabSize = tabSize };

            var src = TestData.GetPath("TestData", "Formatting", filename);

            using (var reader = new StreamReader(src)) {
                var edits = await BlockFormatter.ProvideEdits(reader, position, options);
                edits.Should().OnlyHaveTextEdit("", (line, startCharacter, line, endCharacter));
            }
        }

        [DataRow(6, 22, 0, 2)]
        [DataRow(35, 13, 0, 2)]
        [DataRow(54, 19, 0, 2)]
        [DataRow(76, 9, 0, 2)]
        [DataRow(143, 22, 0, 2)]
        [DataRow(172, 11, 0, 2)]
        [DataRow(195, 12, 0, 2)]
        [DataTestMethod, Priority(0)]
        public async Task TryBlockTwoSpace(int line, int col, int startCharacter, int endCharacter) {
            var position = new Position { line = line, character = col };
            var options = new FormattingOptions { insertSpaces = true, tabSize = 2 };

            var src = TestData.GetPath("TestData", "Formatting", "tryBlocks2.py");

            using (var reader = new StreamReader(src)) {
                var edits = await BlockFormatter.ProvideEdits(reader, position, options);
                edits.Should().OnlyHaveTextEdit("", (line, startCharacter, line, endCharacter));
            }
        }

        [DataRow(15, 21)]
        [DataRow(47, 12)]
        [DataRow(157, 25)]
        [DataTestMethod, Priority(0)]
        public async Task TryBlockTwoSpaceNoEdits(int line, int col) {
            var position = new Position { line = line, character = col };
            var options = new FormattingOptions { insertSpaces = true, tabSize = 2 };

            var src = TestData.GetPath("TestData", "Formatting", "tryBlocks2.py");

            using (var reader = new StreamReader(src)) {
                var edits = await BlockFormatter.ProvideEdits(reader, position, options);
                edits.Should().BeEmpty();
            }
        }

        [DataRow(6, 22, 0, 4)]
        [DataRow(35, 13, 0, 4)]
        [DataRow(54, 19, 0, 4)]
        [DataRow(76, 9, 0, 4)]
        [DataRow(143, 22, 0, 4)]
        [DataRow(172, 11, 0, 4)]
        [DataRow(195, 12, 0, 4)]
        [DataTestMethod, Priority(0)]
        public async Task TryBlockFourSpace(int line, int col, int startCharacter, int endCharacter) {
            var position = new Position { line = line, character = col };
            var options = new FormattingOptions { insertSpaces = true, tabSize = 4 };

            var src = TestData.GetPath("TestData", "Formatting", "tryBlocks4.py");

            using (var reader = new StreamReader(src)) {
                var edits = await BlockFormatter.ProvideEdits(reader, position, options);
                edits.Should().OnlyHaveTextEdit("", (line, startCharacter, line, endCharacter));
            }
        }

        [DataRow(15, 21)]
        [DataRow(47, 12)]
        [DataRow(157, 25)]
        [DataTestMethod, Priority(0)]
        public async Task TryBlockFourSpaceNoEdits(int line, int col) {
            var position = new Position { line = line, character = col };
            var options = new FormattingOptions { insertSpaces = true, tabSize = 4 };

            var src = TestData.GetPath("TestData", "Formatting", "tryBlocks4.py");

            using (var reader = new StreamReader(src)) {
                var edits = await BlockFormatter.ProvideEdits(reader, position, options);
                edits.Should().BeEmpty();
            }
        }

        [DataRow(6, 22, 0, 2)]
        [DataRow(35, 13, 0, 2)]
        [DataRow(54, 19, 0, 2)]
        [DataRow(76, 9, 0, 2)]
        [DataRow(143, 22, 0, 2)]
        [DataRow(172, 11, 0, 3)]
        [DataRow(195, 12, 0, 2)]
        [DataTestMethod, Priority(0)]
        public async Task TryBlockTab(int line, int col, int startCharacter, int endCharacter) {
            var position = new Position { line = line, character = col };
            var options = new FormattingOptions { insertSpaces = false, tabSize = 4 };

            var src = TestData.GetPath("TestData", "Formatting", "tryBlocksTab.py");
            var newText = new string('\t', endCharacter - 1);

            using (var reader = new StreamReader(src)) {
                var edits = await BlockFormatter.ProvideEdits(reader, position, options);
                edits.Should().OnlyHaveTextEdit(newText, (line, startCharacter, line, endCharacter));
            }
        }

        [DataRow(4, 18, 0, 2)]
        [DataRow(7, 18, 0, 2)]
        [DataRow(21, 18, 0, 2)]
        [DataRow(38, 7, 0, 2)]
        [DataRow(47, 13, 0, 2)]
        [DataRow(57, 9, 0, 2)]
        [DataRow(66, 20, 0, 2)]
        [DataRow(69, 20, 0, 2)]
        [DataRow(83, 20, 0, 2)]
        [DataRow(109, 15, 0, 2)]
        [DataRow(119, 11, 0, 2)]
        [DataRow(134, 9, 0, 2)]
        [DataTestMethod, Priority(0)]
        public async Task ElseBlockTwoSpace(int line, int col, int startCharacter, int endCharacter) {
            var position = new Position { line = line, character = col };
            var options = new FormattingOptions { insertSpaces = true, tabSize = 2 };

            var src = TestData.GetPath("TestData", "Formatting", "elseBlocks2.py");

            using (var reader = new StreamReader(src)) {
                var edits = await BlockFormatter.ProvideEdits(reader, position, options);
                edits.Should().OnlyHaveTextEdit("", (line, startCharacter, line, endCharacter));
            }
        }

        [DataRow(345, 18)]
        [DataRow(359, 18)]
        [DataTestMethod, Priority(0)]
        public async Task ElseBlockTwoSpaceNoEdits(int line, int col) {
            var position = new Position { line = line, character = col };
            var options = new FormattingOptions { insertSpaces = true, tabSize = 2 };

            var src = TestData.GetPath("TestData", "Formatting", "elseBlocks2.py");

            using (var reader = new StreamReader(src)) {
                var edits = await BlockFormatter.ProvideEdits(reader, position, options);
                edits.Should().BeEmpty();
            }
        }

        [DataRow(4, 18, 0, 4)]
        [DataRow(7, 18, 0, 4)]
        [DataRow(21, 18, 0, 4)]
        [DataRow(38, 7, 0, 4)]
        [DataRow(47, 13, 0, 4)]
        [DataRow(57, 9, 0, 4)]
        [DataRow(66, 20, 0, 4)]
        [DataRow(69, 20, 0, 4)]
        [DataRow(83, 20, 0, 4)]
        [DataRow(109, 15, 0, 4)]
        [DataRow(119, 11, 0, 4)]
        [DataRow(134, 9, 0, 4)]
        [DataTestMethod, Priority(0)]
        public async Task ElseBlockFourSpace(int line, int col, int startCharacter, int endCharacter) {
            var position = new Position { line = line, character = col };
            var options = new FormattingOptions { insertSpaces = true, tabSize = 4 };

            var src = TestData.GetPath("TestData", "Formatting", "elseBlocks4.py");

            using (var reader = new StreamReader(src)) {
                var edits = await BlockFormatter.ProvideEdits(reader, position, options);
                edits.Should().OnlyHaveTextEdit("", (line, startCharacter, line, endCharacter));
            }
        }

        [DataRow(345, 18)]
        [DataTestMethod, Priority(0)]
        public async Task ElseBlockFourSpaceNoEdits(int line, int col) {
            var position = new Position { line = line, character = col };
            var options = new FormattingOptions { insertSpaces = true, tabSize = 4 };

            var src = TestData.GetPath("TestData", "Formatting", "elseBlocks4.py");

            using (var reader = new StreamReader(src)) {
                var edits = await BlockFormatter.ProvideEdits(reader, position, options);
                edits.Should().BeEmpty();
            }
        }

        [DataRow(4, 18, 0, 1)]
        [DataRow(7, 18, 0, 1)]
        [DataRow(21, 18, 0, 1)]
        [DataRow(38, 7, 0, 1)]
        [DataRow(47, 13, 0, 1)]
        [DataRow(57, 9, 0, 1)]
        [DataRow(66, 20, 0, 1)]
        [DataRow(69, 20, 0, 1)]
        [DataRow(83, 20, 0, 1)]
        [DataRow(109, 15, 0, 1)]
        [DataRow(119, 11, 0, 1)]
        [DataRow(134, 9, 0, 1)]
        [DataTestMethod, Priority(0)]
        public async Task ElseBlockTab(int line, int col, int startCharacter, int endCharacter) {
            var position = new Position { line = line, character = col };
            var options = new FormattingOptions { insertSpaces = true, tabSize = 2 };

            var src = TestData.GetPath("TestData", "Formatting", "elseBlocksTab.py");

            using (var reader = new StreamReader(src)) {
                var edits = await BlockFormatter.ProvideEdits(reader, position, options);
                edits.Should().OnlyHaveTextEdit("", (line, startCharacter, line, endCharacter));
            }
        }

        [DataRow(345, 18)]
        [DataTestMethod, Priority(0)]
        public async Task ElseBlockTabNoEdits(int line, int col) {
            var position = new Position { line = line, character = col };
            var options = new FormattingOptions { insertSpaces = true, tabSize = 2 };

            var src = TestData.GetPath("TestData", "Formatting", "elseBlocksTab.py");

            using (var reader = new StreamReader(src)) {
                var edits = await BlockFormatter.ProvideEdits(reader, position, options);
                edits.Should().BeEmpty();
            }
        }
    }
}
