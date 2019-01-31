using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Python.Analysis.Indexing;
using Microsoft.Python.Core.IO;
using Microsoft.Python.Parsing;
using Microsoft.Python.Parsing.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TestUtilities;

namespace Microsoft.Python.Analysis.Tests {

    [TestClass]
    public class IndexParserTests : AnalysisTestBase {
        private ISymbolIndex _symbolIndex;
        private IFileSystem _fileSystem;
        private PythonLanguageVersion _pythonLanguageVersion;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() {
            TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");
            _symbolIndex = new SymbolIndex();
            _fileSystem = Substitute.For<IFileSystem>();
            _pythonLanguageVersion = PythonVersions.LatestAvailable3X.Version.ToLanguageVersion();
        }

        [TestCleanup]
        public void Cleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public void NullIndexThrowsException() {
            Action build = () => {
                IIndexParser indexParser = new IndexParser(null, _fileSystem, _pythonLanguageVersion);
            };
            build.Should().Throw<ArgumentNullException>();
        }

        [TestMethod, Priority(0)]
        public async Task ParseVariableInFileAsync() {
            const string testFilePath = "C:/bla.py";
            _fileSystem.FileExists(testFilePath).Returns(true);
            _fileSystem.FileOpen(testFilePath, FileMode.Open).Returns(MakeStream("x = 1"));

            IIndexParser indexParser = new IndexParser(_symbolIndex, _fileSystem, _pythonLanguageVersion);
            await indexParser.ParseAsync(testFilePath);

            var symbols = _symbolIndex.WorkspaceSymbols("");
            symbols.Should().HaveCount(1);
            symbols.First().Kind.Should().BeEquivalentTo(SymbolKind.Variable);
            symbols.First().Name.Should().BeEquivalentTo("x");
        }

        [TestMethod, Priority(0)]
        public void ParseNonexistentFile() {
            const string testFilePath = "C:/bla.py";
            _fileSystem.FileExists(testFilePath).Returns(false);

            IIndexParser indexParser = new IndexParser(_symbolIndex, _fileSystem, _pythonLanguageVersion);
            Func<Task> parse = async () => {
                await indexParser.ParseAsync(testFilePath);
            };

            parse.Should().Throw<FileNotFoundException>();
            var symbols = _symbolIndex.WorkspaceSymbols("");
            symbols.Should().HaveCount(0);
        }

        [TestMethod, Priority(0)]
        public void CancellParsing() {
            const string testFilePath = "C:/bla.py";
            _fileSystem.FileExists(testFilePath).Returns(true);
            _fileSystem.FileOpen(testFilePath, FileMode.Open).Returns(MakeStream("x = 1"));

            IIndexParser indexParser = new IndexParser(_symbolIndex, _fileSystem, _pythonLanguageVersion);
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();
            Func<Task> parse = async () => {
                await indexParser.ParseAsync(testFilePath, cancellationTokenSource.Token);
            };

            parse.Should().Throw<TaskCanceledException>();
            var symbols = _symbolIndex.WorkspaceSymbols("");
            symbols.Should().HaveCount(0);
        }

        [TestMethod, Priority(0)]
        public void DisposeParserCancelsParsing() {
            const string testFilePath = "C:/bla.py";
            _fileSystem.FileExists(testFilePath).Returns(true);
            MemoryStream stream = new MemoryStream();
            // no writing to the stream, will block when read
            _fileSystem.FileOpen(testFilePath, FileMode.Open).Returns(stream);

            IIndexParser indexParser = new IndexParser(_symbolIndex, _fileSystem, _pythonLanguageVersion);
            Func<Task> parse = async () => {
                Task t = indexParser.ParseAsync(testFilePath);
                indexParser.Dispose();
                await t;
            };

            parse.Should().Throw<TaskCanceledException>();
            var symbols = _symbolIndex.WorkspaceSymbols("");
            symbols.Should().HaveCount(0);
        }

        private Stream MakeStream(string str) {
            return new MemoryStream(Encoding.UTF8.GetBytes(str));
        }
    }
}
