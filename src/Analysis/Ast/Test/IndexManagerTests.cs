using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Documents;
using Microsoft.Python.Analysis.Indexing;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Ast;
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

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() {
            TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");
            _fileSystem = Substitute.For<IFileSystem>();
            _symbolIndex = new SymbolIndex();
            _rootPath = "C:/root";
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
            string pythonTestFile = $"{_rootPath}/bla.py";
            AddFileToRootTestFileSystem(pythonTestFile);
            _fileSystem.FileOpen(pythonTestFile, FileMode.Open).Returns(MakeStream("x = 1"));

            IIndexManager indexManager = new IndexManager(_symbolIndex, _fileSystem, PythonLanguageVersion.V37, _rootPath, new string[] { }, new string[] { });
            await indexManager.AddRootDirectoryAsync();

            var symbols = _symbolIndex.WorkspaceSymbols("");
            symbols.Should().HaveCount(1);
            symbols.First().Kind.Should().BeEquivalentTo(SymbolKind.Variable);
            symbols.First().Name.Should().BeEquivalentTo("x");
        }

        [TestMethod, Priority(0)]
        public void NullDirectoryThrowsException() {
            Action construct = () => {
                IIndexManager indexManager = new IndexManager(_symbolIndex, _fileSystem, PythonLanguageVersion.V37, null, new string[] { }, new string[] { });
            };
            construct.Should().Throw<ArgumentNullException>();
        }

        [TestMethod, Priority(0)]
        public void IgnoresNonPythonFiles() {
            string nonPythonTestFile = $"{_rootPath}/bla.txt";
            AddFileToRootTestFileSystem(nonPythonTestFile);

            IIndexManager indexManager = new IndexManager(_symbolIndex, _fileSystem, PythonLanguageVersion.V37, this._rootPath, new string[] { }, new string[] { });
            indexManager.AddRootDirectoryAsync();

            _fileSystem.DidNotReceive().FileExists(nonPythonTestFile);
        }

        [TestMethod, Priority(0)]
        public void CanOpenFiles() {
            string nonRootPath = "C:/nonRoot";
            string pythonTestFile = $"{nonRootPath}/bla.py";
            IDocument doc = Substitute.For<IDocument>();
            doc.GetAnyAst().Returns(MakeAst("x = 1"));

            IIndexManager indexManager = new IndexManager(_symbolIndex, _fileSystem, PythonLanguageVersion.V37, _rootPath, new string[] { }, new string[] { });
            indexManager.ProcessNewFile(new Uri(pythonTestFile), doc);

            var symbols = _symbolIndex.WorkspaceSymbols("");
            symbols.Should().HaveCount(1);
            symbols.First().Kind.Should().BeEquivalentTo(SymbolKind.Variable);
            symbols.First().Name.Should().BeEquivalentTo("x");
        }

        [TestMethod, Priority(0)]
        public async Task UpdateFilesOnWorkspaceIndexesLatestAsync() {
            string pythonTestFile = $"{_rootPath}/bla.py";
            AddFileToRootTestFileSystem(pythonTestFile);
            _fileSystem.FileOpen(pythonTestFile, FileMode.Open).Returns(MakeStream("x = 1"));

            IDocument latestDoc = Substitute.For<IDocument>();
            latestDoc.GetAnyAst().Returns(MakeAst("y = 1"));

            IIndexManager indexManager = new IndexManager(_symbolIndex, _fileSystem, PythonLanguageVersion.V37, _rootPath, new string[] { }, new string[] { });
            await indexManager.AddRootDirectoryAsync();
            indexManager.ReIndexFile(new Uri(pythonTestFile), latestDoc);

            var symbols = _symbolIndex.WorkspaceSymbols("");
            symbols.Should().HaveCount(1);
            symbols.First().Kind.Should().BeEquivalentTo(SymbolKind.Variable);
            symbols.First().Name.Should().BeEquivalentTo("y");
        }

        [TestMethod, Priority(0)]
        public async Task CloseNonWorkspaceFilesRemovesFromIndexAsync() {
            string nonRootPath = "C:/nonRoot";
            string pythonTestFile = $"{nonRootPath}/bla.py";
            IDocument doc = Substitute.For<IDocument>();
            doc.GetAnyAst().Returns(MakeAst("x = 1"));

            IIndexManager indexManager = new IndexManager(_symbolIndex, _fileSystem, PythonLanguageVersion.V37, _rootPath, new string[] { }, new string[] { });
            indexManager.ProcessNewFile(new Uri(pythonTestFile), doc);
            await indexManager.ProcessClosedFileAsync(new Uri(pythonTestFile));

            var symbols = _symbolIndex.WorkspaceSymbols("");
            symbols.Should().HaveCount(0);
        }

        [TestMethod, Priority(0)]
        public async Task CloseWorkspaceFilesReUpdatesIndexAsync() {
            string pythonTestFile = $"{_rootPath}/bla.py";
            AddFileToRootTestFileSystem(pythonTestFile);
            _fileSystem.FileOpen(pythonTestFile, FileMode.Open).Returns(MakeStream("x = 1"));
            _fileSystem.IsPathUnderRoot(_rootPath, pythonTestFile).Returns(true);

            IDocument latestDoc = Substitute.For<IDocument>();
            latestDoc.GetAnyAst().Returns(MakeAst("y = 1"));

            IIndexManager indexManager = new IndexManager(_symbolIndex, _fileSystem, PythonLanguageVersion.V37, _rootPath, new string[] { }, new string[] { });
            await indexManager.AddRootDirectoryAsync();
            indexManager.ProcessNewFile(new Uri(pythonTestFile), latestDoc);
            // It Needs to remake the stream for the file, previous one is closed
            _fileSystem.FileOpen(pythonTestFile, FileMode.Open).Returns(MakeStream("x = 1"));
            await indexManager.ProcessClosedFileAsync(new Uri(pythonTestFile));

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
            string pythonTestFile = $"{nonRootPath}/bla.py";
            IDocument doc = Substitute.For<IDocument>();
            doc.GetAnyAst().Returns(MakeAst("x = 1"));

            IIndexManager indexManager = new IndexManager(_symbolIndex, _fileSystem, PythonLanguageVersion.V37, _rootPath, new string[] { }, new string[] { });
            indexManager.ProcessNewFile(new Uri(pythonTestFile), doc);
            await indexManager.ProcessClosedFileAsync(new Uri(pythonTestFile));
            doc.GetAnyAst().Returns(MakeAst("x = 1"));
            indexManager.ReIndexFile(new Uri(pythonTestFile), doc);

            var symbols = _symbolIndex.WorkspaceSymbols("");
            symbols.Should().HaveCount(0);
        }

        private PythonAst MakeAst(string testCode) {
            return Parser.CreateParser(MakeStream(testCode), PythonLanguageVersion.V37).ParseFile();
        }

        private void AddFileToRootTestFileSystem(string filePath) {
            _rootFileList.Add(new FileInfoProxy(new FileInfo(filePath)));
            _fileSystem.FileExists(filePath).Returns(true);
        }

        private Stream MakeStream(string str) {
            return new MemoryStream(Encoding.UTF8.GetBytes(str));
        }
    }
}
