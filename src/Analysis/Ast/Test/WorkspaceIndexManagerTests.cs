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
        private IDirectoryFileReader _rootFileReader;
        private IFileSystem _fileSystem;
        private ISymbolIndex _symbolIndex;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() {
            TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");
            _fileSystem = Substitute.For<IFileSystem>();
            _rootFileReader = Substitute.For<IDirectoryFileReader>();
            _symbolIndex = new SymbolIndex();
        }

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public void AddsRootDirectory() {
            var rootPath = "C:/root";
            string pythonTestFile = $"{rootPath}/bla.py";
            AddFileToTestFileSystem(pythonTestFile);
            _fileSystem.FileOpen(pythonTestFile, FileMode.Open).Returns(MakeStream("x = 1"));

            IWorkspaceIndexManager workspaceIndexManager = new WorkspaceIndexManager(_symbolIndex, _fileSystem, PythonLanguageVersion.V37, _rootFileReader);
            workspaceIndexManager.AddRootDirectory();

            var symbols = _symbolIndex.WorkspaceSymbols("");
            symbols.Should().HaveCount(1);
            symbols.First().Kind.Should().BeEquivalentTo(SymbolKind.Variable);
            symbols.First().Name.Should().BeEquivalentTo("x");
        }

        [TestMethod, Priority(0)]
        public void NullDirectoryThrowsException() {
            Action construct = () => {
                IWorkspaceIndexManager workspaceIndexManager = new WorkspaceIndexManager(_symbolIndex, _fileSystem, PythonLanguageVersion.V37, null);
            };
            construct.Should().Throw<ArgumentNullException>();
        }

        [TestMethod, Priority(0)]
        public void IgnoresNonPythonFiles() {
            var rootPath = "C:/root";
            string nonPythonTestFile = $"{rootPath}/bla.txt";
            AddFileToTestFileSystem(nonPythonTestFile);

            IWorkspaceIndexManager workspaceIndexManager = new WorkspaceIndexManager(_symbolIndex, _fileSystem, PythonLanguageVersion.V37, _rootFileReader);
            workspaceIndexManager.AddRootDirectory();

            _fileSystem.DidNotReceive().FileExists(nonPythonTestFile);
        }

        private void AddFileToTestFileSystem(string filePath) {
            _rootFileReader.DirectoryFilePaths().Returns(new List<string>() {
                filePath
            });
            _fileSystem.FileExists(filePath).Returns(true);
        }

        private Stream MakeStream(string str) {
            return new MemoryStream(Encoding.UTF8.GetBytes(str));
        }

    }
}
