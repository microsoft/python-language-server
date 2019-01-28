using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using Microsoft.Python.Analysis.Indexing;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {
    [TestClass]
    public class WorkspaceIndexManagerTests : AnalysisTestBase {
        private IFileSystem _fileSystem;
        private ISymbolIndex _symbolIndex;
        private string _rootPath;
        private List<IFileSystemInfo> _fileList;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() {
            TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");
            _fileSystem = Substitute.For<IFileSystem>();
            _symbolIndex = new SymbolIndex();
            _rootPath = "C:/root";
            _fileList = new List<IFileSystemInfo>();
            IDirectoryInfo directoryInfo = Substitute.For<IDirectoryInfo>();
            // Doesn't work without 'forAnyArgs'
            directoryInfo.EnumerateFileSystemInfos(new string[] { }, new string[] { }).ReturnsForAnyArgs(_fileList);
            _fileSystem.GetDirectoryInfo(_rootPath).Returns(directoryInfo);
        }

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public void AddsRootDirectory() {
            string pythonTestFile = $"{_rootPath}/bla.py";
            AddFileToTestFileSystem(pythonTestFile);
            _fileSystem.FileOpen(pythonTestFile, FileMode.Open).Returns(MakeStream("x = 1"));

            IWorkspaceIndexManager workspaceIndexManager = new WorkspaceIndexManager(_symbolIndex, _fileSystem, PythonLanguageVersion.V37, _rootPath, new string[] { }, new string[] { });
            workspaceIndexManager.AddRootDirectory();

            var symbols = _symbolIndex.WorkspaceSymbols("");
            symbols.Should().HaveCount(1);
            symbols.First().Kind.Should().BeEquivalentTo(SymbolKind.Variable);
            symbols.First().Name.Should().BeEquivalentTo("x");
        }

        [TestMethod, Priority(0)]
        public void NullDirectoryThrowsException() {
            Action construct = () => {
                IWorkspaceIndexManager workspaceIndexManager = new WorkspaceIndexManager(_symbolIndex, _fileSystem, PythonLanguageVersion.V37, null, new string[] { }, new string[] { });
            };
            construct.Should().Throw<ArgumentNullException>();
        }

        [TestMethod, Priority(0)]
        public void IgnoresNonPythonFiles() {
            string nonPythonTestFile = $"{_rootPath}/bla.txt";
            AddFileToTestFileSystem(nonPythonTestFile);

            IWorkspaceIndexManager workspaceIndexManager = new WorkspaceIndexManager(_symbolIndex, _fileSystem, PythonLanguageVersion.V37, this._rootPath, new string[] { }, new string[] { });
            workspaceIndexManager.AddRootDirectory();

            _fileSystem.DidNotReceive().FileExists(nonPythonTestFile);
        }

        private void AddFileToTestFileSystem(string filePath) {
            _fileList.Add(new FileInfoProxy(new FileInfo(filePath)));
            _fileSystem.FileExists(filePath).Returns(true);
        }

        private Stream MakeStream(string str) {
            return new MemoryStream(Encoding.UTF8.GetBytes(str));
        }

    }
}
