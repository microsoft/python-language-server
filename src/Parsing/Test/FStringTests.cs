using System;
using System.IO;
using System.Text;
using Microsoft.Python.Parsing.Ast;
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
        public void ParsesFStringExpression() {
            var parser = Parser.CreateParser(MakeStream("f'bla'"), PythonLanguageVersion.V36);
            var ast = parser.ParseFile();
            ast.Walk(new MyPythonWalker(fExpr => {
                return true;
            }));
        }

        public Stream MakeStream(string s) => new MemoryStream(Encoding.UTF8.GetBytes(s));
    }

    internal class MyPythonWalker : PythonWalker {
        private readonly Func<FStringExpression, bool> _t;
        public MyPythonWalker(Func<FStringExpression, bool> t) {
            _t = t;
        }

        public override bool Walk(FStringExpression node) {
            return _t(node);
        }

        public override bool Walk(ConstantExpression node) {
            throw new InternalTestFailureException();
        }
    }
}
