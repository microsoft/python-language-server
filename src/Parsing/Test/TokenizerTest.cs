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

        private Tokenizer MakeTokenizer(PythonLanguageVersion version, TokenizerOptions optionSet, string text, 
            SourceLocation? initialSourceLocation = null) {
            var tokenizer = new Tokenizer(version, options: optionSet);

            tokenizer.Initialize(null, new StringReader(text), initialSourceLocation ?? SourceLocation.MinValue);
            return tokenizer;
        }

    }
}
