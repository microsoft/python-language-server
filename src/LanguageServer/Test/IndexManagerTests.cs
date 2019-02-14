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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Documents;
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
    public class IndexManagerTests : LanguageServerTestBase {
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
            FileWithXVarInRootDir();
            AddFileToRoot($"{_rootPath}\foo.py", MakeStream("y = 1"));

            var indexManager = GetDefaultIndexManager();
            await indexManager.AddRootDirectoryAsync();

            var symbols = _symbolIndex.WorkspaceSymbols("");
            symbols.Should().HaveCount(2);
        }

        [TestMethod, Priority(0)]
        public void NullDirectoryThrowsException() {
            Action construct = () => {
                PythonLanguageVersion version = PythonVersions.LatestAvailable3X.Version.ToLanguageVersion();
                IIndexManager indexManager = new IndexManager(_symbolIndex, _fileSystem,
                                                              version, null, new string[] { }, new string[] { });
            };
            construct.Should().Throw<ArgumentNullException>();
        }

        [TestMethod, Priority(0)]
        public void IgnoresNonPythonFiles() {
            var nonPythonTestFileInfo = MakeFileInfoProxy($"{_rootPath}/bla.txt");
            AddFileInfoToRootTestFS(nonPythonTestFileInfo);

            IIndexManager indexManager = new IndexManager(_symbolIndex, _fileSystem, PythonVersions.LatestAvailable3X.Version.ToLanguageVersion(), this._rootPath, new string[] { }, new string[] { });
            indexManager.AddRootDirectoryAsync();

            _fileSystem.DidNotReceive().FileExists(nonPythonTestFileInfo.FullName);
        }

        [TestMethod, Priority(0)]
        public async Task CanOpenFiles() {
            string nonRootPath = "C:/nonRoot";
            var pythonTestFileInfo = MakeFileInfoProxy($"{nonRootPath}/bla.py");
            const string TestCode = "x = 1";
            IDocument doc = DocumentWithAst(TestCode);

            IIndexManager indexManager = GetDefaultIndexManager();
            await indexManager.ProcessNewFile(pythonTestFileInfo.FullName, doc);

            var symbols = _symbolIndex.WorkspaceSymbols("");
            SymbolsShouldBeOnlyX(symbols);
        }

        private IDocument DocumentWithAst(string TestCode) {
            IDocument doc = Substitute.For<IDocument>();
            doc.GetAstAsync().Returns(Task.FromResult(MakeAst(TestCode)));
            return doc;
        }

        [TestMethod, Priority(0)]
        public async Task UpdateFilesOnWorkspaceIndexesLatestAsync() {
            var pythonTestFilePath = FileWithXVarInRootDir();
            IDocument latestDoc = DocumentWithAst("y = 1");

            var indexManager = GetDefaultIndexManager();
            await indexManager.AddRootDirectoryAsync();
            indexManager.ReIndexFile(pythonTestFilePath, latestDoc);

            var symbols = _symbolIndex.WorkspaceSymbols("");
            symbols.Should().HaveCount(1);
            symbols.First().Kind.Should().BeEquivalentTo(SymbolKind.Variable);
            symbols.First().Name.Should().BeEquivalentTo("y");
        }

        [TestMethod, Priority(0)]
        public async Task CloseNonWorkspaceFilesRemovesFromIndexAsync() {
            var pythonTestFileInfo = MakeFileInfoProxy("C:/nonRoot/bla.py");
            IDocument doc = Substitute.For<IDocument>();
            doc.GetAstAsync().Returns(Task.FromResult(MakeAst("x = 1")));

            var indexManager = GetDefaultIndexManager();
            await indexManager.ProcessNewFile(pythonTestFileInfo.FullName, doc);
            await indexManager.ProcessClosedFileAsync(pythonTestFileInfo.FullName);

            SymbolIndexShouldBeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task CloseWorkspaceFilesReUpdatesIndexAsync() {
            string pythonTestFilePath = FileWithXVarInRootDir();
            _fileSystem.IsPathUnderRoot(_rootPath, pythonTestFilePath).Returns(true);

            IDocument latestDoc = DocumentWithAst("y = 1");

            var indexManager = GetDefaultIndexManager();
            await indexManager.AddRootDirectoryAsync();
            await indexManager.ProcessNewFile(pythonTestFilePath, latestDoc);
            // It Needs to remake the stream for the file, previous one is closed
            _fileSystem.FileOpen(pythonTestFilePath, FileMode.Open).Returns(MakeStream("x = 1"));
            await indexManager.ProcessClosedFileAsync(pythonTestFilePath);

            var symbols = _symbolIndex.WorkspaceSymbols("");
            SymbolsShouldBeOnlyX(symbols);
        }

        [TestMethod, Priority(0)]
        public async Task ProcessFileIfIndexedAfterCloseIgnoresUpdateAsync() {
            // If events get to index manager in the order: [open, close, update]
            // it should not reindex file

            var pythonTestFileInfo = MakeFileInfoProxy("C:/nonRoot/bla.py");
            IDocument doc = DocumentWithAst("x = 1");

            var indexManager = GetDefaultIndexManager();
            await indexManager.ProcessNewFile(pythonTestFileInfo.FullName, doc);
            await indexManager.ProcessClosedFileAsync(pythonTestFileInfo.FullName);
            doc = DocumentWithAst("x = 1");
            await indexManager.ReIndexFile(pythonTestFileInfo.FullName, doc);

            SymbolIndexShouldBeEmpty();
        }

        [TestMethod, Priority(0)]
        public void AddingRootMightThrowUnauthorizedAccess() {
            string pythonTestFilePath = FileWithXVarInRootDir();
            _fileSystem.GetDirectoryInfo(_rootPath).EnumerateFileSystemInfos(new string[] { }, new string[] { })
                .ReturnsForAnyArgs(_ => throw new UnauthorizedAccessException());

            var indexManager = GetDefaultIndexManager();
            Func<Task> add = async () => {
                await indexManager.AddRootDirectoryAsync();
            };

            add.Should().Throw<UnauthorizedAccessException>();
            SymbolIndexShouldBeEmpty();
        }

        private void SymbolIndexShouldBeEmpty() {
            var symbols = _symbolIndex.WorkspaceSymbols("");
            symbols.Should().HaveCount(0);
        }

        [TestMethod, Priority(0)]
        public void DisposeManagerCancelsTask() {
            string pythonTestFilePath = FileWithXVarInRootDir();
            ManualResetEventSlim neverSignaledEvent = new ManualResetEventSlim(false);
            ManualResetEventSlim fileOpenedEvent = new ManualResetEventSlim(false);

            _fileSystem.FileOpen(pythonTestFilePath, FileMode.Open).Returns(_ => {
                fileOpenedEvent.Set();
                // Wait forever
                neverSignaledEvent.Wait();
                throw new InternalTestFailureException("Task should have been cancelled");
            });

            var indexManager = GetDefaultIndexManager();
            fileOpenedEvent.Wait();
            indexManager.Dispose();

            Func<Task> add = async () => {
                await indexManager.AddRootDirectoryAsync();
            };

            add.Should().Throw<TaskCanceledException>();
            SymbolIndexShouldBeEmpty();
        }

        [TestMethod, Priority(0)]
        public async Task WorkspaceSymbolsAddsRootDirectory() {
            string pythonTestFilePath = FileWithXVarInRootDir();

            var indexManager = GetDefaultIndexManager();

            var symbols = await indexManager.WorkspaceSymbolsAsync("", 10);
            SymbolsShouldBeOnlyX(symbols);
        }

        [TestMethod, Priority(0)]
        public async Task WorkspaceSymbolsLimited() {
            for (int fileNumber = 0; fileNumber < 10; fileNumber++) {
                AddFileToRoot($"{_rootPath}\bla{fileNumber}.py", MakeStream($"x{fileNumber} = 1"));
            }
            var indexManager = GetDefaultIndexManager();

            const int amountOfSymbols = 3;

            var symbols = await indexManager.WorkspaceSymbolsAsync("", amountOfSymbols);
            symbols.Should().HaveCount(amountOfSymbols);
        }

        [TestMethod, Priority(0)]
        public async Task HierarchicalDocumentSymbolsAsync() {
            string pythonTestFilePath = FileWithXVarInRootDir();

            var indexManager = GetDefaultIndexManager();

            var symbols = await indexManager.HierarchicalDocumentSymbolsAsync(pythonTestFilePath);
            SymbolsShouldBeOnlyX(symbols);
        }

        [TestMethod, Priority(0)]
        public async Task LatestVersionASTVersionIsIndexed() {
            ManualResetEventSlim reOpenedFileFinished = new ManualResetEventSlim(false);
            ManualResetEventSlim fileOpenedEvent = new ManualResetEventSlim(false);

            var pythonTestFilePath = FileWithXVarInRootDir();
            _fileSystem.FileOpen(pythonTestFilePath, FileMode.Open).Returns(_ => {
                fileOpenedEvent.Set();
                // Wait forever
                reOpenedFileFinished.Wait();
                return MakeStream("x = 1");
            });

            var indexManager = GetDefaultIndexManager();

            IDocument yVarDoc = DocumentWithAst("y = 1");
            IDocument zVarDoc = DocumentWithAst("z = 1");

            await indexManager.ProcessNewFile(pythonTestFilePath, yVarDoc);
            var closeFileTask = indexManager.ProcessClosedFileAsync(pythonTestFilePath);
            fileOpenedEvent.Wait();
            await indexManager.ProcessNewFile(pythonTestFilePath, zVarDoc);
            reOpenedFileFinished.Set();

            await closeFileTask;
            var symbols = await indexManager.HierarchicalDocumentSymbolsAsync(pythonTestFilePath);
            symbols.Should().HaveCount(1);
            symbols.First().Kind.Should().BeEquivalentTo(SymbolKind.Variable);
            symbols.First().Name.Should().BeEquivalentTo("z");
        }

        private static void SymbolsShouldBeOnlyX(IEnumerable<HierarchicalSymbol> symbols) {
            symbols.Should().HaveCount(1);
            symbols.First().Kind.Should().BeEquivalentTo(SymbolKind.Variable);
            symbols.First().Name.Should().BeEquivalentTo("x");
        }

        private static void SymbolsShouldBeOnlyX(IEnumerable<FlatSymbol> symbols) {
            symbols.Should().HaveCount(1);
            symbols.First().Kind.Should().BeEquivalentTo(SymbolKind.Variable);
            symbols.First().Name.Should().BeEquivalentTo("x");
        }

        private PythonAst MakeAst(string testCode) {
            return Parser.CreateParser(MakeStream(testCode), PythonVersions.LatestAvailable3X.Version.ToLanguageVersion()).ParseFile();
        }

        private FileInfoProxy MakeFileInfoProxy(string filePath)
            => new FileInfoProxy(new FileInfo(filePath));

        private void AddFileInfoToRootTestFS(FileInfoProxy fileInfo) {
            _rootFileList.Add(fileInfo);
            _fileSystem.FileExists(fileInfo.FullName).Returns(true);
        }

        private Stream MakeStream(string str) {
            return new MemoryStream(Encoding.UTF8.GetBytes(str));
        }

        private string FileWithXVarInRootDir() {
            return AddFileToRoot($"{_rootPath}\bla.py", MakeStream("x = 1"));
        }

        private IIndexManager GetDefaultIndexManager() {
            return new IndexManager(_symbolIndex, _fileSystem, _pythonLanguageVersion,
                                    _rootPath, new string[] { }, new string[] { });
        }

        private string AddFileToRoot(string filePath, Stream stream) {
            var fileInfo = MakeFileInfoProxy(filePath);
            AddFileInfoToRootTestFS(fileInfo);
            string fullName = fileInfo.FullName;
            _fileSystem.FileOpen(fullName, FileMode.Open).Returns(stream);
            // FileInfo fullName is used everywhere as path
            // Otherwise, path discrepancies might appear
            return fullName;
        }
    }
}
