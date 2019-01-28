using System;
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
    public class IndexParserTests : AnalysisTestBase {
        public TestContext TestContext { get; set; }
        ISymbolIndex _symbolIndex;
        IFileSystem _fileSystem;

        [TestInitialize]
        public void TestInitialize() {
            TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");
            _symbolIndex = new SymbolIndex();
            _fileSystem = Substitute.For<IFileSystem>();
        }

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();
        [TestMethod, Priority(0)]
        public void NoSymbols() {
            ISymbolIndex symbolIndex = new SymbolIndex();
            var symbols = symbolIndex.WorkspaceSymbols("");
            symbols.Should().BeEmpty();
        }

        [TestMethod, Priority(0)]
        public void NullIndexThrowsException() {
            Action build = () => {
                IIndexParser indexParser = new IndexParser(null, _fileSystem, PythonLanguageVersion.V37);
            };
            build.Should().Throw<ArgumentNullException>();
        }

        [TestMethod, Priority(0)]
        public void ParseVariableInFile() {
            const string testFilePath = "C:/bla.py";
            _fileSystem.FileExists(testFilePath).Returns(true);
            _fileSystem.FileOpen(testFilePath, FileMode.Open).Returns(MakeStream("x = 1"));

            IIndexParser indexParser = new IndexParser(_symbolIndex, _fileSystem, PythonLanguageVersion.V37);
            indexParser.ParseFile(new Uri(testFilePath));

            var symbols = _symbolIndex.WorkspaceSymbols("");
            symbols.Should().HaveCount(1);
            symbols.First().Kind.Should().BeEquivalentTo(SymbolKind.Variable);
            symbols.First().Name.Should().BeEquivalentTo("x");
        }

        [TestMethod, Priority(0)]
        public void ParseNonexistentFile() {
            const string testFilePath = "C:/bla.py";
            _fileSystem.FileExists(testFilePath).Returns(false);

            IIndexParser indexParser = new IndexParser(_symbolIndex, _fileSystem, PythonLanguageVersion.V37);
            Action parse = () => {
                indexParser.ParseFile(new Uri(testFilePath));
            };
            parse.Should().Throw<FileNotFoundException>();
        }

        private Stream MakeStream(string str) {
            return new MemoryStream(Encoding.UTF8.GetBytes(str));
        }
    }
}
