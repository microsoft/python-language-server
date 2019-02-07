using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace Microsoft.Python.Parsing.Tests {
    [TestClass]
    public class FStringTests {
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() => TestEnvironmentImpl.TestInitialize($"{TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}");

        [TestCleanup]
        public void TestCleanup() => TestEnvironmentImpl.TestCleanup();

        [TestMethod, Priority(0)]
        public void Bla() {
            var parser = Parser.CreateParser(MakeStream("f'bla"), PythonLanguageVersion.V36);
            var ast = parser.ParseFile();
        }

        public Stream MakeStream(string s) => new MemoryStream(Encoding.UTF8.GetBytes(s));
    }
}
