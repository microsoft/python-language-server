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
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Core.Idle;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Core.Services;
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
        private readonly int maxSymbolsCount = 1000;
        private const string _rootPath = "C:/root";

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() {
            TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");
        }

        [TestCleanup]
        public void Cleanup() {
            TestEnvironmentImpl.TestCleanup();
        }

        [TestMethod, Priority(0)]
        public async Task AddsRootDirectoryAsync() {
            var context = new IndexTestContext(this);
            context.FileWithXVarInRootDir();
            context.AddFileToRoot($"{_rootPath}\foo.py", MakeStream("y = 1"));

            var indexManager = context.GetDefaultIndexManager();
            await indexManager.IndexWorkspace();

            var symbols = await indexManager.WorkspaceSymbolsAsync("", maxSymbolsCount);
            symbols.Should().HaveCount(2);
        }

        [TestMethod, Priority(0)]
        public async Task IgnoresNonPythonFiles() {
            var context = new IndexTestContext(this);

            var nonPythonTestFileInfo = MakeFileInfoProxy($"{_rootPath}/bla.txt");
            context.AddFileInfoToRootTestFS(nonPythonTestFileInfo);

            IIndexManager indexManager = context.GetDefaultIndexManager();
            await indexManager.IndexWorkspace();

            context.FileSystem.DidNotReceive().FileExists(nonPythonTestFileInfo.FullName);
        }

        [TestMethod, Priority(0)]
        public async Task CanOpenFiles() {
            string nonRootPath = "C:/nonRoot";
            var context = new IndexTestContext(this);
            var pythonTestFileInfo = MakeFileInfoProxy($"{nonRootPath}/bla.py");
            IDocument doc = DocumentWithAst("x = 1");

            IIndexManager indexManager = context.GetDefaultIndexManager();
            indexManager.ProcessNewFile(pythonTestFileInfo.FullName, doc);

            var symbols = await indexManager.WorkspaceSymbolsAsync("", maxSymbolsCount);
            SymbolsShouldBeOnlyX(symbols);
        }

        [TestMethod, Priority(0)]
        public async Task UpdateFilesOnWorkspaceIndexesLatestAsync() {
            var context = new IndexTestContext(this);
            var pythonTestFilePath = context.FileWithXVarInRootDir();

            var indexManager = context.GetDefaultIndexManager();
            await indexManager.IndexWorkspace();

            indexManager.ReIndexFile(pythonTestFilePath, DocumentWithAst("y = 1"));

            var symbols = await indexManager.WorkspaceSymbolsAsync("", maxSymbolsCount);
            symbols.Should().HaveCount(1);
            symbols.First().Kind.Should().BeEquivalentTo(SymbolKind.Variable);
            symbols.First().Name.Should().BeEquivalentTo("y");
        }

        [TestMethod, Priority(0)]
        public async Task CloseNonWorkspaceFilesRemovesFromIndexAsync() {
            var context = new IndexTestContext(this);
            var pythonTestFileInfo = MakeFileInfoProxy("C:/nonRoot/bla.py");
            context.FileSystem.IsPathUnderRoot(_rootPath, pythonTestFileInfo.FullName).Returns(false);

            var indexManager = context.GetDefaultIndexManager();
            indexManager.ProcessNewFile(pythonTestFileInfo.FullName, DocumentWithAst("x = 1"));
            indexManager.ProcessClosedFile(pythonTestFileInfo.FullName);

            await SymbolIndexShouldBeEmpty(indexManager);
        }

        [TestMethod, Priority(0)]
        public async Task CloseWorkspaceFilesReUpdatesIndexAsync() {
            var context = new IndexTestContext(this);
            var pythonTestFilePath = context.FileWithXVarInRootDir();
            context.FileSystem.IsPathUnderRoot(_rootPath, pythonTestFilePath).Returns(true);

            var indexManager = context.GetDefaultIndexManager();
            await indexManager.IndexWorkspace();

            indexManager.ProcessNewFile(pythonTestFilePath, DocumentWithAst("r = 1"));
            // It Needs to remake the stream for the file, previous one is closed
            context.FileSystem.FileExists(pythonTestFilePath).Returns(true);
            context.SetFileOpen(pythonTestFilePath, MakeStream("x = 1"));
            context.FileSystem.IsPathUnderRoot(_rootPath, pythonTestFilePath).Returns(true);
            indexManager.ProcessClosedFile(pythonTestFilePath);

            var symbols = await indexManager.WorkspaceSymbolsAsync("", maxSymbolsCount);
            SymbolsShouldBeOnlyX(symbols);
        }

        [TestMethod, Priority(0)]
        public async Task ProcessFileIfIndexedAfterCloseIgnoresUpdateAsync() {
            // If events get to index manager in the order: [open, close, update]
            // it should not reindex file

            var context = new IndexTestContext(this);
            var pythonTestFileInfo = MakeFileInfoProxy("C:/nonRoot/bla.py");

            var indexManager = context.GetDefaultIndexManager();
            indexManager.ProcessNewFile(pythonTestFileInfo.FullName, DocumentWithAst("x = 1"));
            indexManager.ProcessClosedFile(pythonTestFileInfo.FullName);

            context.FileSystem.IsPathUnderRoot(_rootPath, pythonTestFileInfo.FullName).Returns(false);
            indexManager.ReIndexFile(pythonTestFileInfo.FullName, DocumentWithAst("x = 1"));

            await SymbolIndexShouldBeEmpty(indexManager);
        }

        [TestMethod, Priority(0)]
        public async Task WorkspaceSymbolsAddsRootDirectory() {
            var context = new IndexTestContext(this);

            var pythonTestFilePath = context.FileWithXVarInRootDir();

            var indexManager = context.GetDefaultIndexManager();
            await indexManager.IndexWorkspace();

            var symbols = await indexManager.WorkspaceSymbolsAsync("", maxSymbolsCount);
            SymbolsShouldBeOnlyX(symbols);
        }

        [TestMethod, Priority(0)]
        public async Task WorkspaceSymbolsLimited() {
            var context = new IndexTestContext(this);

            for (int fileNumber = 0; fileNumber < 10; fileNumber++) {
                context.AddFileToRoot($"{_rootPath}\bla{fileNumber}.py", MakeStream($"x{fileNumber} = 1"));
            }
            var indexManager = context.GetDefaultIndexManager();
            await indexManager.IndexWorkspace();

            const int amountOfSymbols = 3;

            var symbols = await indexManager.WorkspaceSymbolsAsync("", amountOfSymbols);
            symbols.Should().HaveCount(amountOfSymbols);
        }

        [TestMethod, Priority(0)]
        public async Task HierarchicalDocumentSymbolsAsync() {
            var context = new IndexTestContext(this);
            var pythonTestFilePath = context.FileWithXVarInRootDir();

            var indexManager = context.GetDefaultIndexManager();
            await indexManager.IndexWorkspace();

            var symbols = await indexManager.HierarchicalDocumentSymbolsAsync(pythonTestFilePath);
            SymbolsShouldBeOnlyX(symbols);
        }

        [TestMethod, Priority(0)]
        public async Task LatestVersionASTVersionIsIndexed() {
            var context = new IndexTestContext(this);
            var pythonTestFilePath = context.FileWithXVarInRootDir();

            var indexManager = context.GetDefaultIndexManager();
            indexManager.ProcessNewFile(pythonTestFilePath, DocumentWithAst("y = 1"));
            indexManager.ProcessClosedFile(pythonTestFilePath);
            indexManager.ProcessNewFile(pythonTestFilePath, DocumentWithAst("z = 1"));

            var symbols = await indexManager.HierarchicalDocumentSymbolsAsync(pythonTestFilePath);
            symbols.Should().HaveCount(1);
            symbols.First().Kind.Should().BeEquivalentTo(SymbolKind.Variable);
            symbols.First().Name.Should().BeEquivalentTo("z");
        }

        [TestMethod, Priority(0)]
        public async Task AddFilesToPendingChanges() {
            var context = new IndexTestContext(this);
            var f1 = context.AddFileToRoot($"{_rootPath}/fileA.py", MakeStream(""));
            var f2 = context.AddFileToRoot($"{_rootPath}/fileB.py", MakeStream(""));

            var indexManager = context.GetDefaultIndexManager();
            await indexManager.IndexWorkspace();

            indexManager.AddPendingDoc(DocumentWithAst("y = 1", f1));
            indexManager.AddPendingDoc(DocumentWithAst("x = 1", f2));

            context.SetIdleEvent(Raise.Event());

            var symbols = await indexManager.WorkspaceSymbolsAsync("", maxSymbolsCount);
            symbols.Should().HaveCount(2);
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


        private class IndexTestContext : IDisposable {
            private readonly List<IFileSystemInfo> _rootFileList = new List<IFileSystemInfo>();
            private readonly IIdleTimeService _idleTimeService = Substitute.For<IIdleTimeService>();
            private readonly PythonLanguageVersion _pythonLanguageVersion = PythonVersions.LatestAvailable3X.Version.ToLanguageVersion();
            private IIndexManager _indexM;
            private IndexManagerTests _tests;

            public IndexTestContext(IndexManagerTests tests) {
                _tests = tests;
                Setup();
            }

            private void Setup() {
                FileSystem = Substitute.For<IFileSystem>();
                SymbolIndex = new SymbolIndex(FileSystem, _pythonLanguageVersion);
                SetupRootDir();
            }

            public IFileSystem FileSystem { get; private set; }
            public SymbolIndex SymbolIndex { get; private set; }

            public void AddFileInfoToRootTestFS(FileInfoProxy fileInfo) {
                _rootFileList.Add(fileInfo);
                FileSystem.FileExists(fileInfo.FullName).Returns(true);
            }

            public string FileWithXVarInRootDir() {
                return AddFileToRoot($"{_rootPath}\bla.py", _tests.MakeStream("x = 1"));
            }

            public IIndexManager GetDefaultIndexManager() {
                _indexM = new IndexManager(FileSystem, _pythonLanguageVersion,
                                        _rootPath, new string[] { }, new string[] { },
                                        _idleTimeService) {
                    ReIndexingDelay = 1
                };

                return _indexM;
            }

            public string AddFileToRoot(string filePath, Stream stream) {
                var fileInfo = _tests.MakeFileInfoProxy(filePath);
                AddFileInfoToRootTestFS(fileInfo);
                SetFileOpen(fileInfo.FullName, stream);
                // FileInfo fullName is used everywhere as path
                // Otherwise, path discrepancies might appear
                return fileInfo.FullName;
            }

            public void SetIdleEvent(EventHandler<EventArgs> handler) {
                _idleTimeService.Idle += handler;
            }

            private void SetupRootDir() {
                var directoryInfo = Substitute.For<IDirectoryInfo>();
                directoryInfo.Match("", new string[] { }, new string[] { }).ReturnsForAnyArgs(callInfo => {
                    string path = callInfo.ArgAt<string>(0);
                    return _rootFileList
                        .Where(fsInfo => PathEqualityComparer.Instance.Equals(fsInfo.FullName, path))
                        .Count() > 0;
                });
                // Doesn't work without 'forAnyArgs'
                directoryInfo.EnumerateFileSystemInfos(new string[] { }, new string[] { }).ReturnsForAnyArgs(_rootFileList);
                FileSystem.GetDirectoryInfo(_rootPath).Returns(directoryInfo);
            }

            public void Dispose() {
                _indexM?.Dispose();
            }

            public void SetFileOpen(string pythonTestFilePath, Func<object, Stream> returnFunc) {
                FileSystem.FileOpen(pythonTestFilePath, FileMode.Open, FileAccess.Read, FileShare.Read).Returns(returnFunc);
            }

            internal void SetFileOpen(string path, Stream stream) {
                FileSystem.FileOpen(path, FileMode.Open, FileAccess.Read, FileShare.Read).Returns(stream);
            }
        }

        private IDocument DocumentWithAst(string testCode, string filePath = null) {
            filePath = filePath ?? $"{_rootPath}/{testCode}.py";
            IDocument doc = Substitute.For<IDocument>();
            doc.GetAstAsync().ReturnsForAnyArgs(Task.FromResult(MakeAst(testCode)));
            doc.Uri.Returns(new Uri(filePath));
            return doc;
        }

        private async Task SymbolIndexShouldBeEmpty(IIndexManager indexManager) {
            var symbols = await indexManager.WorkspaceSymbolsAsync("", maxSymbolsCount);
            symbols.Should().HaveCount(0);
        }

        public PythonAst MakeAst(string testCode) {
            PythonLanguageVersion latestVersion = PythonVersions.LatestAvailable3X.Version.ToLanguageVersion();
            return Parser.CreateParser(MakeStream(testCode), latestVersion).ParseFile();
        }

        public Stream MakeStream(string str) {
            return new MemoryStream(Encoding.UTF8.GetBytes(str));
        }

        public FileInfoProxy MakeFileInfoProxy(string filePath)
            => new FileInfoProxy(new FileInfo(filePath));
    }
}
