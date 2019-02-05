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
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Indexing;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class IndexManagerTests : AnalysisTestBase {
        private IFileSystem _fileSystem;
        private ISymbolIndex _symbolIndex;
        private string _rootPath;
        private List<IFileSystemInfo> _rootFileList;
        private PythonLanguageVersion _pythonLanguageVersion;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() {
            TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");
            _fileSystem = Substitute.For<IFileSystem>();
            _symbolIndex = new SymbolIndex();
            _rootPath = "C:\root";
            _pythonLanguageVersion = PythonVersions.LatestAvailable3X.Version.ToLanguageVersion();
            _rootFileList = new List<IFileSystemInfo>();
            IDirectoryInfo directoryInfo = Substitute.For<IDirectoryInfo>();
            // Doesn't work without 'forAnyArgs'
            directoryInfo.EnumerateFileSystemInfos(new string[] { }, new string[] { }).ReturnsForAnyArgs(_rootFileList);
            _fileSystem.GetDirectoryInfo(_rootPath).Returns(directoryInfo);
        }

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public async Task AddsRootDirectoryAsync() {
            var pythonTestFileInfo = MakeFileInfoProxy($"{_rootPath}\bla.py");
            AddFileToRootTestFileSystem(pythonTestFileInfo);
            var fooFile = MakeFileInfoProxy($"{_rootPath}\foo.py");
            AddFileToRootTestFileSystem(fooFile);
            _fileSystem.FileOpen(pythonTestFileInfo.FullName, FileMode.Open).Returns(MakeStream("x = 1"));
            _fileSystem.FileOpen(fooFile.FullName, FileMode.Open).Returns(MakeStream("y = 1"));

            IIndexManager indexManager = new IndexManager(_symbolIndex, _fileSystem, _pythonLanguageVersion, _rootPath, new string[] { }, new string[] { });
            await indexManager.AddRootDirectoryAsync();

            var symbols = _symbolIndex.WorkspaceSymbols("x");
            symbols.Should().HaveCount(1);
            symbols.First().Kind.Should().BeEquivalentTo(SymbolKind.Variable);
            symbols.First().Name.Should().BeEquivalentTo("x");
        }

        [TestMethod, Priority(0)]
        public void NullDirectoryThrowsException() {
            Action construct = () => {
                IIndexManager indexManager = new IndexManager(_symbolIndex, _fileSystem, PythonVersions.LatestAvailable3X.Version.ToLanguageVersion(), null, new string[] { }, new string[] { });
            };
            construct.Should().Throw<ArgumentNullException>();
        }

        [TestMethod, Priority(0)]
        public void IgnoresNonPythonFiles() {
            var nonPythonTestFileInfo = MakeFileInfoProxy($"{_rootPath}/bla.txt");
            AddFileToRootTestFileSystem(nonPythonTestFileInfo);

            IIndexManager indexManager = new IndexManager(_symbolIndex, _fileSystem, PythonVersions.LatestAvailable3X.Version.ToLanguageVersion(), this._rootPath, new string[] { }, new string[] { });
            indexManager.AddRootDirectoryAsync();

            _fileSystem.DidNotReceive().FileExists(nonPythonTestFileInfo.FullName);
        }

        [TestMethod, Priority(0)]
        public void CanOpenFiles() {
            string nonRootPath = "C:/nonRoot";
            var pythonTestFileInfo = MakeFileInfoProxy($"{nonRootPath}/bla.py");
            IDocument doc = Substitute.For<IDocument>();
            doc.GetAnyAst().Returns(MakeAst("x = 1"));

            IIndexManager indexManager = new IndexManager(_symbolIndex, _fileSystem, PythonVersions.LatestAvailable3X.Version.ToLanguageVersion(), _rootPath, new string[] { }, new string[] { });
            indexManager.ProcessNewFile(pythonTestFileInfo.FullName, doc);

            var symbols = _symbolIndex.WorkspaceSymbols("");
            symbols.Should().HaveCount(1);
            symbols.First().Kind.Should().BeEquivalentTo(SymbolKind.Variable);
            symbols.First().Name.Should().BeEquivalentTo("x");
        }

        [TestMethod, Priority(0)]
        public async Task UpdateFilesOnWorkspaceIndexesLatestAsync() {
            var pythonTestFileInfo = MakeFileInfoProxy($"{_rootPath}/bla.py");
            AddFileToRootTestFileSystem(pythonTestFileInfo);
            _fileSystem.FileOpen(pythonTestFileInfo.FullName, FileMode.Open).Returns(MakeStream("x = 1"));

            IDocument latestDoc = Substitute.For<IDocument>();
            latestDoc.GetAnyAst().Returns(MakeAst("y = 1"));

            IIndexManager indexManager = new IndexManager(_symbolIndex, _fileSystem, PythonVersions.LatestAvailable3X.Version.ToLanguageVersion(), _rootPath, new string[] { }, new string[] { });
            await indexManager.AddRootDirectoryAsync();
            indexManager.ReIndexFile(pythonTestFileInfo.FullName, latestDoc);

            var symbols = _symbolIndex.WorkspaceSymbols("");
            symbols.Should().HaveCount(1);
            symbols.First().Kind.Should().BeEquivalentTo(SymbolKind.Variable);
            symbols.First().Name.Should().BeEquivalentTo("y");
        }

        [TestMethod, Priority(0)]
        public async Task CloseNonWorkspaceFilesRemovesFromIndexAsync() {
            string nonRootPath = "C:/nonRoot";
            var pythonTestFileInfo = MakeFileInfoProxy($"{nonRootPath}/bla.py");
            IDocument doc = Substitute.For<IDocument>();
            doc.GetAnyAst().Returns(MakeAst("x = 1"));

            IIndexManager indexManager = new IndexManager(_symbolIndex, _fileSystem, PythonVersions.LatestAvailable3X.Version.ToLanguageVersion(), _rootPath, new string[] { }, new string[] { });
            indexManager.ProcessNewFile(pythonTestFileInfo.FullName, doc);
            await indexManager.ProcessClosedFileAsync(pythonTestFileInfo.FullName);

            var symbols = _symbolIndex.WorkspaceSymbols("");
            symbols.Should().HaveCount(0);
        }

        [TestMethod, Priority(0)]
        public async Task CloseWorkspaceFilesReUpdatesIndexAsync() {
            var pythonTestFile = MakeFileInfoProxy($"{_rootPath}/bla.py");
            AddFileToRootTestFileSystem(pythonTestFile);
            _fileSystem.FileOpen(pythonTestFile.FullName, FileMode.Open).Returns(MakeStream("x = 1"));
            _fileSystem.IsPathUnderRoot(_rootPath, pythonTestFile.FullName).Returns(true);

            IDocument latestDoc = Substitute.For<IDocument>();
            latestDoc.GetAnyAst().Returns(MakeAst("y = 1"));

            IIndexManager indexManager = new IndexManager(_symbolIndex, _fileSystem, PythonVersions.LatestAvailable3X.Version.ToLanguageVersion(), _rootPath, new string[] { }, new string[] { });
            await indexManager.AddRootDirectoryAsync();
            indexManager.ProcessNewFile(pythonTestFile.FullName, latestDoc);
            // It Needs to remake the stream for the file, previous one is closed
            _fileSystem.FileOpen(pythonTestFile.FullName, FileMode.Open).Returns(MakeStream("x = 1"));
            await indexManager.ProcessClosedFileAsync(pythonTestFile.FullName);

            var symbols = _symbolIndex.WorkspaceSymbols("");
            symbols.Should().HaveCount(1);
            symbols.First().Kind.Should().BeEquivalentTo(SymbolKind.Variable);
            symbols.First().Name.Should().BeEquivalentTo("x");
        }

        [TestMethod, Priority(0)]
        public async Task ProcessFileIfIndexedAfterCloseIgnoresUpdateAsync() {
            // If events get to index manager in the order: [open, close, update]
            // it should not reindex file

            string nonRootPath = "C:/nonRoot";
            var pythonTestFileInfo = MakeFileInfoProxy($"{nonRootPath}/bla.py");
            IDocument doc = Substitute.For<IDocument>();
            doc.GetAnyAst().Returns(MakeAst("x = 1"));

            IIndexManager indexManager = new IndexManager(_symbolIndex, _fileSystem, PythonVersions.LatestAvailable3X.Version.ToLanguageVersion(), _rootPath, new string[] { }, new string[] { });
            indexManager.ProcessNewFile(pythonTestFileInfo.FullName, doc);
            await indexManager.ProcessClosedFileAsync(pythonTestFileInfo.FullName);
            doc.GetAnyAst().Returns(MakeAst("x = 1"));
            indexManager.ReIndexFile(pythonTestFileInfo.FullName, doc);

            var symbols = _symbolIndex.WorkspaceSymbols("");
            symbols.Should().HaveCount(0);
        }

        [TestMethod, Priority(0)]
        public void AddingRootMightThrowUnauthorizedAccess() {
            var pythonTestFileInfo = MakeFileInfoProxy($"{_rootPath}\bla.py");
            AddFileToRootTestFileSystem(pythonTestFileInfo);
            _fileSystem.GetDirectoryInfo(_rootPath).EnumerateFileSystemInfos(new string[] { }, new string[] { })
                .ReturnsForAnyArgs(_ => throw new UnauthorizedAccessException());
            _fileSystem.FileOpen(pythonTestFileInfo.FullName, FileMode.Open).Returns(MakeStream("x = 1"));

            IIndexManager indexManager = new IndexManager(_symbolIndex, _fileSystem, _pythonLanguageVersion, _rootPath, new string[] { }, new string[] { });
            Func<Task> add = async () => {
                await indexManager.AddRootDirectoryAsync();
            };

            add.Should().Throw<UnauthorizedAccessException>();
            var symbols = _symbolIndex.WorkspaceSymbols("");
            symbols.Should().HaveCount(0);
        }

        [TestMethod, Priority(0)]
        public void DisposeManagerCancelsTask() {
            var pythonTestFileInfo = MakeFileInfoProxy($"{_rootPath}\bla.py");
            AddFileToRootTestFileSystem(pythonTestFileInfo);
            ManualResetEventSlim neverSignaledEvent = new ManualResetEventSlim(false);
            ManualResetEventSlim fileOpenedEvent = new ManualResetEventSlim(false);

            _fileSystem.FileOpen(pythonTestFileInfo.FullName, FileMode.Open).Returns(_ => {
                fileOpenedEvent.Set();
                // Wait forever
                neverSignaledEvent.Wait();
                throw new InternalTestFailureException("Task should have been cancelled");
            });

            IIndexManager indexManager = new IndexManager(_symbolIndex, _fileSystem, _pythonLanguageVersion, _rootPath, new string[] { }, new string[] { });

            fileOpenedEvent.Wait();
            indexManager.Dispose();

            Func<Task> add = async () => {
                await indexManager.AddRootDirectoryAsync();
            };

            add.Should().Throw<TaskCanceledException>();
            var symbols = _symbolIndex.WorkspaceSymbols("");
            symbols.Should().HaveCount(0);
        }

        [TestMethod, Priority(0)]
        public async Task WorkspaceSymbolsAddsRootDirectory() {
            var pythonTestFileInfo = MakeFileInfoProxy($"{_rootPath}\bla.py");
            AddFileToRootTestFileSystem(pythonTestFileInfo);
            _fileSystem.FileOpen(pythonTestFileInfo.FullName, FileMode.Open).Returns(MakeStream("x = 1"));

            IIndexManager indexManager = new IndexManager(_symbolIndex, _fileSystem, _pythonLanguageVersion, _rootPath, new string[] { }, new string[] { });

            var symbols = await indexManager.WorkspaceSymbolsAsync("", 10);
            symbols.Should().HaveCount(1);
            symbols.First().Kind.Should().BeEquivalentTo(SymbolKind.Variable);
            symbols.First().Name.Should().BeEquivalentTo("x");
        }

        [TestMethod, Priority(0)]
        public async Task WorkspaceSymbolsLimited() {
            for (int fileNumber = 0; fileNumber < 10; fileNumber++) {
                var pythonTestFileInfo = MakeFileInfoProxy($"{_rootPath}\bla{fileNumber}.py");
                AddFileToRootTestFileSystem(pythonTestFileInfo);
                _fileSystem.FileOpen(pythonTestFileInfo.FullName, FileMode.Open).Returns(MakeStream($"x{fileNumber} = 1"));
            }
            IIndexManager indexManager = new IndexManager(_symbolIndex, _fileSystem, _pythonLanguageVersion, _rootPath, new string[] { }, new string[] { });

            const int amountOfSymbols = 3;
            await indexManager.WorkspaceSymbolsAsync("", amountOfSymbols);

            var symbols = indexManager.WorkspaceSymbolsAsync("", amountOfSymbols).Result;
            symbols.Should().HaveCount(amountOfSymbols);
        }

        private PythonAst MakeAst(string testCode) {
            return Parser.CreateParser(MakeStream(testCode), PythonVersions.LatestAvailable3X.Version.ToLanguageVersion()).ParseFile();
        }

        private FileInfoProxy MakeFileInfoProxy(string filePath)
            => new FileInfoProxy(new FileInfo(filePath));

        private void AddFileToRootTestFileSystem(FileInfoProxy fileInfo) {
            _rootFileList.Add(fileInfo);
            _fileSystem.FileExists(fileInfo.FullName).Returns(true);
        }

        private Stream MakeStream(string str) {
            return new MemoryStream(Encoding.UTF8.GetBytes(str));
        }
    }
}
