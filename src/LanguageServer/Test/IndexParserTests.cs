﻿// Copyright(c) Microsoft Corporation
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Core.IO;
using Microsoft.Python.LanguageServer.Indexing;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TestUtilities;

namespace Microsoft.Python.LanguageServer.Tests {

    [TestClass]
    public class IndexParserTests : LanguageServerTestBase {
        private IFileSystem _fileSystem;
        private PythonLanguageVersion _pythonLanguageVersion;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() {
            TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");
            _fileSystem = Substitute.For<IFileSystem>();
            _pythonLanguageVersion = PythonVersions.LatestAvailable3X.Version.ToLanguageVersion();
        }

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task ParseVariableInFileAsync() {
            const string testFilePath = "C:/bla.py";
            _fileSystem.FileExists(testFilePath).Returns(true);

            using (var fileStream = MakeStream("x = 1")) {
                SetFileOpen(_fileSystem, testFilePath, fileStream);
                using (IIndexParser indexParser = new IndexParser(_fileSystem, _pythonLanguageVersion)) {
                    var ast = await indexParser.ParseAsync(testFilePath);

                    var symbols = GetIndexSymbols(ast);
                    symbols.Should().HaveCount(1);
                    symbols.First().Kind.Should().BeEquivalentTo(SymbolKind.Variable);
                    symbols.First().Name.Should().BeEquivalentTo("x");
                }
            }
        }

        private IReadOnlyList<HierarchicalSymbol> GetIndexSymbols(PythonAst ast) {
            var walker = new SymbolIndexWalker(ast);
            ast.Walk(walker);
            return walker.Symbols;
        }


        [TestMethod, Priority(0)]
        [ExpectedException(typeof(FileNotFoundException))]
        public async Task ParseFileThatStopsExisting() {
            const string testFilePath = "C:/bla.py";
            _fileSystem.FileExists(testFilePath).Returns(true);
            SetFileOpen(_fileSystem, testFilePath, _ => throw new FileNotFoundException());

            using (var indexParser = new IndexParser(_fileSystem, _pythonLanguageVersion)) {
                await indexParser.ParseAsync(testFilePath);
            }
        }

        [TestMethod, Priority(0)]
        public void CancelParsingAsync() {
            const string testFilePath = "C:/bla.py";
            _fileSystem.FileExists(testFilePath).Returns(true);

            using (var fileStream = MakeStream("x = 1")) {
                SetFileOpen(_fileSystem, testFilePath, fileStream);
            }

            using (var indexParser = new IndexParser(_fileSystem, _pythonLanguageVersion))
            using (var cancellationTokenSource = new CancellationTokenSource()) {
                cancellationTokenSource.Cancel();

                Func<Task> parse = async () => {
                    await indexParser.ParseAsync(testFilePath, cancellationTokenSource.Token);
                };
                parse.Should().Throw<TaskCanceledException>();
            }
        }

        private void SetFileOpen(IFileSystem fileSystem, string path, Stream stream) {
            fileSystem.FileOpen(path, FileMode.Open, FileAccess.Read, FileShare.Read).Returns(stream);
        }

        private void SetFileOpen(IFileSystem fileSystem, string path, Func<object, Stream> p) 
            => fileSystem.FileOpen(path, FileMode.Open, FileAccess.Read, FileShare.Read).Returns(p);

        private Stream MakeStream(string str) 
            => new MemoryStream(Encoding.UTF8.GetBytes(str));
    }
}
