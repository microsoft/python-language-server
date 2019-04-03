using System.IO;
using FluentAssertions;
using Microsoft.Python.Core.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Parsing.Tests {
    /// <summary>
    /// Test cases to verify that the tokenizer maintains expected behaviour.
    /// </summary>
    [TestClass]
    public class TokenizerTests {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void TestCleanup() => TestEnvironmentImpl.TestCleanup();

        internal static readonly PythonLanguageVersion[] AllVersions = new[] { PythonLanguageVersion.V26, PythonLanguageVersion.V27, PythonLanguageVersion.V30, PythonLanguageVersion.V31, PythonLanguageVersion.V32, PythonLanguageVersion.V33, PythonLanguageVersion.V34, PythonLanguageVersion.V35, PythonLanguageVersion.V36, PythonLanguageVersion.V37 };

        [TestMethod, Priority(0)]
        public void AbsoluteTokenizerIndex() {
            foreach (var version in AllVersions) {
                var tokenizer = MakeTokenizer(version, TokenizerOptions.None, "x = 1", new SourceLocation(5, 100, 3));

                // Read 'x' token
                var tokenInfo = tokenizer.ReadToken();
                tokenizer.TokenSpan.Should().Be(new IndexSpan(5, 1));
                tokenizer.CurrentPosition.Should().BeEquivalentTo(new SourceLocation(6, 100, 4));

                // Read '=' token
                tokenInfo = tokenizer.ReadToken();
                tokenizer.TokenSpan.Should().Be(new IndexSpan(7, 1));
                tokenizer.CurrentPosition.Should().BeEquivalentTo(new SourceLocation(8, 100, 6));
            }
        }

        [TestMethod, Priority(0)]
        public void CRLFNewLines() {
            foreach (var version in AllVersions) {
                var code = "\r\nx\r\ny\r\n";

                var initialLocation = SourceLocation.MinValue;
                var tokenizer = MakeTokenizer(version, TokenizerOptions.None, code,
                    initialLocation);

                CheckAndReadNext(tokenizer, new IndexSpan(0, 2), TokenKind.NLToken);
                CheckAndReadNext(tokenizer, new IndexSpan(2, 1), TokenKind.Name);
                CheckAndReadNext(tokenizer, new IndexSpan(3, 2), TokenKind.NewLine);
                CheckAndReadNext(tokenizer, new IndexSpan(5, 1), TokenKind.Name);
                CheckAndReadNext(tokenizer, new IndexSpan(6, 2), TokenKind.NewLine);
            }
        }

        [TestMethod, Priority(0)]
        public void LFNewLines() {
            foreach (var version in AllVersions) {
                var code = "\nx\ny\n";

                var initialLocation = SourceLocation.MinValue;
                var tokenizer = MakeTokenizer(version, TokenizerOptions.None, code,
                    initialLocation);

                CheckAndReadNext(tokenizer, new IndexSpan(0, 1), TokenKind.NLToken);
                CheckAndReadNext(tokenizer, new IndexSpan(1, 1), TokenKind.Name);
                CheckAndReadNext(tokenizer, new IndexSpan(2, 1), TokenKind.NewLine);
                CheckAndReadNext(tokenizer, new IndexSpan(3, 1), TokenKind.Name);
                CheckAndReadNext(tokenizer, new IndexSpan(4, 1), TokenKind.NewLine);
            }
        }

        private static void CheckAndReadNext(Tokenizer tokenizer, IndexSpan tokenSpan, TokenKind tokenKind) {
            var token = tokenizer.GetNextToken();
            tokenizer.TokenSpan.Should().Be(tokenSpan);
            token.Kind.Should().Be(tokenKind);
        }

        private Tokenizer MakeTokenizer(PythonLanguageVersion version, TokenizerOptions optionSet, string text,
            SourceLocation? initialSourceLocation = null) {
            return MakeTokenizer(version, optionSet, new StringReader(text), initialSourceLocation);
        }

        private Tokenizer MakeTokenizer(PythonLanguageVersion version, TokenizerOptions optionSet, StringReader reader,
            SourceLocation? initialSourceLocation = null) {
            var tokenizer = new Tokenizer(version, options: optionSet);

            tokenizer.Initialize(null, reader, initialSourceLocation ?? SourceLocation.MinValue);
            return tokenizer;
        }

        /* == operator doesn't compare indexes */
        private void ShouldEqual(SourceLocation s1, SourceLocation s2) {
            s1.Index.Should().Be(s2.Index);
            s1.Line.Should().Be(s2.Line);
            s1.Column.Should().Be(s2.Column);
        }
    }
}
